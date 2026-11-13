using System;
using System.Collections.Generic;

namespace ABSmartly.JsonExpressions;

// Semantic version comparison used by the semver_* operators, as the JavaScript and Java SDKs: a
// leading "v"/"V" and build metadata are ignored, missing core parts count as 0, numeric identifiers
// compare numerically (leading zeros ignored) and rank below alphanumeric ones, and a release ranks
// above any of its pre-releases. Parsing is as permissive as the JavaScript SDK's, so every SDK
// orders the same inputs the same way: a non-numeric core part ("1.x") is compared as an
// alphanumeric identifier, and an empty pre-release ("1.0.0-") is treated as a release.
internal static class SemverComparator
{
    // -1, 0 or 1, or null when either side is not a string, number or boolean, or has an empty
    // version core once a leading v/V and build metadata are removed ("", "v", "+build", "-beta").
    internal static int? Compare(IEvaluator evaluator, object lhs, object rhs)
    {
        // StringConvert formats numbers with the invariant culture, so 2.5 is "2.5" (two core
        // parts) whatever the current culture is.
        var lhsString = evaluator.StringConvert(lhs);
        var rhsString = evaluator.StringConvert(rhs);
        return lhsString == null || rhsString == null ? null : Compare(lhsString, rhsString);
    }

    internal static int? Compare(string lhs, string rhs)
    {
        var left = Parse(lhs);
        var right = Parse(rhs);
        if (left == null || right == null) return null;

        var max = Math.Max(left.Core.Count, right.Core.Count);
        for (var i = 0; i < max; i++)
        {
            var result = CompareIdentifier(i < left.Core.Count ? left.Core[i] : "0",
                i < right.Core.Count ? right.Core[i] : "0");
            if (result != 0) return result;
        }

        if (left.PreRelease.Count == 0 && right.PreRelease.Count == 0) return 0;
        if (left.PreRelease.Count == 0) return 1;
        if (right.PreRelease.Count == 0) return -1;

        var preMax = Math.Max(left.PreRelease.Count, right.PreRelease.Count);
        for (var i = 0; i < preMax; i++)
        {
            if (i >= left.PreRelease.Count) return -1;
            if (i >= right.PreRelease.Count) return 1;
            var result = CompareIdentifier(left.PreRelease[i], right.PreRelease[i]);
            if (result != 0) return result;
        }

        return 0;
    }

    private static VersionParts Parse(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.StartsWith("v", StringComparison.Ordinal) || value.StartsWith("V", StringComparison.Ordinal)) value = value.Substring(1);

        var plus = value.IndexOf('+');
        if (plus >= 0) value = value.Substring(0, plus);
        if (value.Length == 0) return null;

        var dash = value.IndexOf('-');
        var core = dash >= 0 ? value.Substring(0, dash) : value;
        var preRelease = dash >= 0 ? value.Substring(dash + 1) : "";
        if (core.Length == 0) return null;

        return new VersionParts(new List<string>(core.Split('.')), preRelease.Length == 0
            ? new List<string>()
            : new List<string>(preRelease.Split('.')));
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = IsNumeric(left);
        var rightNumeric = IsNumeric(right);
        if (leftNumeric && rightNumeric)
        {
            left = StripLeadingZeros(left);
            right = StripLeadingZeros(right);
            var length = left.Length.CompareTo(right.Length);
            return length != 0 ? length : string.CompareOrdinal(left, right).CompareTo(0);
        }

        if (leftNumeric) return -1;
        if (rightNumeric) return 1;
        return string.CompareOrdinal(left, right).CompareTo(0);
    }

    private static bool IsNumeric(string value)
    {
        if (value.Length == 0) return false;
        foreach (var c in value)
            if (c < '0' || c > '9') return false;
        return true;
    }

    private static string StripLeadingZeros(string value)
    {
        var index = 0;
        while (index < value.Length - 1 && value[index] == '0') index++;
        return value.Substring(index);
    }

    private sealed class VersionParts
    {
        internal VersionParts(List<string> core, List<string> preRelease)
        {
            Core = core;
            PreRelease = preRelease;
        }

        internal List<string> Core { get; }
        internal List<string> PreRelease { get; }
    }
}
