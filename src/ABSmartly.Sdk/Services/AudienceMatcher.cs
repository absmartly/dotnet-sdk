using System;
using System.Collections.Generic;
using System.Globalization;
using ABSmartly.JsonExpressions;
using Microsoft.Extensions.Logging;

namespace ABSmartly.Services;

public class AudienceMatcher
{
    // The backend accepts a split when its gap from 100, rounded half up to two decimals, is at
    // most 0.01 (JavaScript: parseFloat(gap.toFixed(2)) <= 0.01). Rounding the exact binary gap that
    // way gives at most 0.01 exactly when the gap is not above the double nearest 0.015 (which is
    // just below 0.015), so comparing with that double is the same check without decimal rounding.
    private const double SplitPercentagesMaxGap = 0.015;

    private readonly IAudienceDeserializer _audienceDeserializer;

    public AudienceMatcher(IAudienceDeserializer audienceDeserializer)
    {
        _audienceDeserializer = audienceDeserializer;
    }

    public bool? Evaluate(string audience, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrWhiteSpace(audience))
            return null;

        var audienceMap = _audienceDeserializer.Deserialize(audience);

        if (audienceMap is null)
            return null;

        if (!audienceMap.TryGetValue("filter", out var filter))
            return null;

        if (filter is not Dictionary<string, object> && filter is not List<object>)
            return null;

        var result = JsonExpr.EvaluateBooleanExpr(filter, attributes);
        return result;
    }

    // Parses an experiment's assignmentRules JSON string; done once when the context data is
    // installed. Null when there are no rules (a null or empty string), Invalid when the string
    // is not a JSON object, None when it has no "rules" list.
    internal ParsedRules ParseRules(string assignmentRules)
    {
        if (string.IsNullOrEmpty(assignmentRules)) return null;

        Dictionary<string, object> rulesMap;
        try
        {
            rulesMap = _audienceDeserializer.Deserialize(assignmentRules);
        }
        catch (Exception)
        {
            // A custom deserializer may throw; the default one logs and returns null.
            return ParsedRules.Invalid;
        }

        if (rulesMap == null) return ParsedRules.Invalid;

        return rulesMap.TryGetValue("rules", out var rules) && rules is List<object> list
            ? new ParsedRules(list)
            : ParsedRules.None;
    }

    internal RuleAction EvaluateRules(string assignmentRules, string environmentName,
        Dictionary<string, object> attributes) =>
        EvaluateRules(ParseRules(assignmentRules), environmentName, attributes, null, null);

    // Returns the action of the first matching rule, or null. As the JavaScript and Java SDKs, a
    // rule is skipped when it is not an object, its type is unknown, an "assign" rule's variant is
    // not an integer, a "split" rule's percentages are invalid, its environments are present but
    // not a list, its non-empty environments list does not contain environmentName (a null
    // environment never matches a non-empty list), its conditions are not an object, or its
    // conditions do not evaluate to true (conditions that throw are logged and skipped). A rule
    // without conditions always matches and an empty environments list matches every environment.
    // Variant bounds are the caller's responsibility.
    internal RuleAction EvaluateRules(ParsedRules parsed, string environmentName,
        Dictionary<string, object> attributes, string experimentName, ILogger logger)
    {
        if (parsed?.Rules == null) return null;

        foreach (var ruleObj in parsed.Rules)
        {
            if (ruleObj is not Dictionary<string, object> rule) continue;

            var action = ParseRuleAction(rule);
            if (action == null) continue;

            if (rule.TryGetValue("environments", out var environments) && environments != null)
            {
                if (environments is not List<object> envs) continue;
                if (envs.Count > 0 && (environmentName == null || !envs.Contains(environmentName))) continue;
            }

            if (!rule.TryGetValue("conditions", out var conditions) || conditions == null) return action;

            if (conditions is not Dictionary<string, object>) continue;

            try
            {
                if (JsonExpr.EvaluateBooleanExpr(conditions, attributes) == true) return action;
            }
            catch (Exception e)
            {
                logger?.LogWarning(e,
                    "Failed to evaluate assignment rule conditions for experiment {Experiment}, rule {Rule}",
                    experimentName, rule.TryGetValue("name", out var name) ? name : null);
            }
        }

        return null;
    }

    private static RuleAction ParseRuleAction(Dictionary<string, object> rule)
    {
        rule.TryGetValue("type", out var type);
        switch (type as string)
        {
            case "assign":
                return rule.TryGetValue("variant", out var variant) && AsInteger(variant) is { } v
                    ? RuleAction.Assign(v)
                    : null;
            case "split":
                return rule.TryGetValue("percentages", out var percentages) &&
                       ParseSplitPercentages(percentages) is { } split
                    ? RuleAction.ForSplit(split)
                    : null;
            default:
                return null;
        }
    }

    // As JavaScript's Number.isInteger: integral numbers (including 2.0) within int range. Strings
    // and booleans are not numbers.
    private static int? AsInteger(object value)
    {
        double d;
        switch (value)
        {
            case int i:
                return i;
            case long l:
                return l is >= int.MinValue and <= int.MaxValue ? (int)l : null;
            case double dbl:
                d = dbl;
                break;
            case float f:
                d = f;
                break;
            case decimal m:
                d = (double)m;
                break;
            default:
                return null;
        }

        if (double.IsNaN(d) || double.IsInfinity(d) || d != Math.Floor(d) || d > int.MaxValue || d < int.MinValue)
            return null;
        return (int)d;
    }

    // Each value is parseFloat(value) / 100, as the JavaScript SDK, with no normalisation; any
    // rounding remainder goes to the last variant when the unit is assigned. Null when the
    // percentages are not a string, a value is not a number or negative, or they do not sum to 100.
    internal static double[] ParseSplitPercentages(object percentages)
    {
        if (percentages is not string text) return null;

        var parts = text.Split('/');
        var values = new double[parts.Length];
        var total = 0.0;
        for (var i = 0; i < parts.Length; ++i)
        {
            var value = ParseFloat(parts[i]);
            if (double.IsNaN(value) || value < 0) return null;
            values[i] = value;
            total += value;
        }

        var gap = Math.Abs(100 - total);
        if (double.IsNaN(gap) || gap > SplitPercentagesMaxGap) return null;

        for (var i = 0; i < values.Length; ++i) values[i] /= 100;
        return values;
    }

    // JavaScript's parseFloat: leading whitespace is skipped and the longest decimal prefix is
    // parsed ("20%" is 20, "1e" is 1); NaN when there is none. "Infinity" parses as infinity, which
    // the sum check then rejects.
    internal static double ParseFloat(string value)
    {
        var s = value.TrimStart();
        var i = 0;
        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;

        if (string.CompareOrdinal(s, i, "Infinity", 0, 8) == 0)
            return s[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;

        var digits = 0;
        while (i < s.Length && IsAsciiDigit(s[i]))
        {
            i++;
            digits++;
        }

        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && IsAsciiDigit(s[i]))
            {
                i++;
                digits++;
            }
        }

        if (digits == 0) return double.NaN;

        var end = i;
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            var j = i + 1;
            if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
            var exponentStart = j;
            while (j < s.Length && IsAsciiDigit(s[j])) j++;
            if (j > exponentStart) end = j;
        }

        var prefix = s.Substring(0, end);
        if (double.TryParse(prefix, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        // Out of range on .NET Framework (netstandard2.0): keep the sign so the sum check rejects the split.
        return prefix[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    internal sealed class ParsedRules
    {
        internal static readonly ParsedRules Invalid = new(null, true);
        internal static readonly ParsedRules None = new(null, false);

        internal ParsedRules(List<object> rules) : this(rules, false)
        {
        }

        private ParsedRules(List<object> rules, bool isInvalid)
        {
            Rules = rules;
            IsInvalid = isInvalid;
        }

        internal List<object> Rules { get; }
        internal bool IsInvalid { get; }
    }

    // The action of the first matching assignment rule: a fixed variant, or (IsSplit) a split to
    // randomize the unit over. A split only comes from ParseSplitPercentages, so it is always a
    // list of non-negative fractions summing to 1.
    internal sealed class RuleAction
    {
        private readonly double[] _split;

        private RuleAction(int variant, double[] split)
        {
            Variant = variant;
            _split = split;
        }

        internal int Variant { get; }
        internal bool IsSplit => _split != null;
        internal double[] Split => (double[])_split?.Clone();

        internal static RuleAction Assign(int variant) => new(variant, null);
        internal static RuleAction ForSplit(double[] split) => new(0, (double[])split.Clone());

        public override string ToString() =>
            IsSplit ? $"RuleAction{{split=[{string.Join(", ", _split)}]}}" : $"RuleAction{{variant={Variant}}}";
    }
}
