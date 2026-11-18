#!/usr/bin/env python3
# Usage: python3 tools/switchback-mutants.py [name-filter ...]. Restores every file after each mutant.
# DOTNET_ROOT defaults to ~/.dotnet when set up as in CI; otherwise uses the dotnet on PATH.
# Mutation probes for the switchback port: each mutant must make the full test suite fail, except
# those in EQUIVALENT, which cannot change behaviour (reported as such, not as gaps).
import subprocess, sys, os, pathlib
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
env = dict(os.environ, DOTNET_ROOT=os.path.expanduser('~/.dotnet'),
           PATH=os.path.expanduser('~/.dotnet') + ':' + os.environ['PATH'], DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
S = 'src/ABSmartly.Sdk/'
M = [
 ('cluster order: walk sources in reverse', S+'Internal/SwitchbackServing.cs', 'foreach (var source in sources)', 'foreach (var source in System.Linq.Enumerable.Reverse(sources))'),
 ('cluster order: sort sources by unit type', S+'Internal/SwitchbackServing.cs', 'foreach (var source in sources)', 'foreach (var source in System.Linq.Enumerable.OrderBy(sources, s => s.Key, StringComparer.Ordinal))'),
 ('cluster order: decoder sorts keys', S+'Services/Json/SwitchbackConverters.cs', 'return clusters.ToArray();', 'clusters.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key)); return clusters.ToArray();'),
 ('cluster order: decoder via Dictionary(ordinal hash)', S+'Services/Json/SwitchbackConverters.cs', 'return clusters.ToArray();', 'return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(clusters, c => c.Key.GetHashCode()));'),
 ('raw key: hash cluster key with MD5', S+'Context.cs', 'SwitchbackAssigner.Assign(serving.ClusterKey, period,', 'SwitchbackAssigner.Assign(ABSmartly.Internal.Hashing.Md5.Hash(serving.ClusterKey), period,'),
 ('raw key: draw over unit uid', S+'Context.cs', 'SwitchbackAssigner.Assign(serving.ClusterKey, period,', 'SwitchbackAssigner.Assign(_units[unitType], period,'),
 ('raw key: draw over hashed uid', S+'Context.cs', 'SwitchbackAssigner.Assign(serving.ClusterKey, period,', 'SwitchbackAssigner.Assign(Md5.Hash(_units[unitType]), period,'),
 ('value rule: whole double keys as integer', S+'Internal/SwitchbackClusterKeys.cs', '            case ulong u:', '            case double d when d == System.Math.Floor(d):\n                return ((long)d).ToString(CultureInfo.InvariantCulture);\n            case ulong u:'),
 ('value rule: JSON 3.0 is integer', S+'Internal/SwitchbackClusterKeys.cs', 'if (raw.IndexOfAny(FloatMarkers) >= 0) return null;', 'if (raw.IndexOfAny(FloatMarkers) >= 0) return element.GetDouble() == System.Math.Floor(element.GetDouble()) ? ((long)element.GetDouble()).ToString(CultureInfo.InvariantCulture) : null;'),
 ('value rule: nested whole float written as int', S+'Internal/SwitchbackAttributePath.cs', 'if (text.IndexOfAny(FloatMarkers) < 0) text += ".0";', ''),
 ('value rule: bool is key', S+'Internal/SwitchbackClusterKeys.cs', '            case ulong u:', '            case bool b:\n                return b ? "true" : "false";\n            case ulong u:'),
 ('value rule: empty string is key', S+'Internal/SwitchbackClusterKeys.cs', 'return s.Length == 0 ? null : s;', 'return s;'),
 ('value rule: integer is not key', S+'Internal/SwitchbackClusterKeys.cs', 'case sbyte or byte or short or ushort or int or uint or long:\n                return System.Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);', 'case sbyte or byte or short or ushort or int or uint or long:\n                return null;'),
 ('value rule: current culture', S+'Internal/SwitchbackClusterKeys.cs', 'System.Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)', 'System.Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture)'),
 ('value rule: object text is key', S+'Internal/SwitchbackClusterKeys.cs', '            case JsonElement element:\n                return Normalize(element);\n            default:\n                return null;', '            case JsonElement element:\n                return Normalize(element);\n            default:\n                return value?.ToString();'),
 ('value rule: JSON object raw text is key', S+'Internal/SwitchbackClusterKeys.cs', '                    .ToString(CultureInfo.InvariantCulture);\n            default:\n                return null;', '                    .ToString(CultureInfo.InvariantCulture);\n            default:\n                return element.GetRawText();'),
 ('epoch: slot type not checked', S+'Context.cs', 'IsSwitchback(experiment) == (assignment.Switchback != null);', 'true;'),
 ('epoch(gen): design type not identity', S+'Context.cs', 'string.Equals(a.SwitchbackDesignType, b.SwitchbackDesignType, StringComparison.Ordinal) &&', ''),
 ('epoch(gen): cluster map not identity', S+'Context.cs', 'ArrayEquality.Equals(a.SwitchbackClusters, b.SwitchbackClusters) &&', ''),
 ('epoch(gen): allow-list not identity', S+'Context.cs', 'ArrayEquality.Equals(a.SwitchbackClusterValues, b.SwitchbackClusterValues) &&', ''),
 ('epoch(gen): start not identity', S+'Context.cs', 'a.SwitchbackStartAt == b.SwitchbackStartAt &&', ''),
 ('epoch(gen): period not identity', S+'Context.cs', 'a.SwitchbackPeriodMs == b.SwitchbackPeriodMs &&', ''),
 ('epoch(gen): iteration not identity', S+'Context.cs', 'return a.Id == b.Id && a.Iteration == b.Iteration &&', 'return a.Id == b.Id &&'),
 ('period: truncating division', S+'Context.cs', 'return SwitchbackAssigner.FloorDiv(now - startAt, experiment.Data.SwitchbackPeriodMs!.Value);', 'return (now - startAt) / experiment.Data.SwitchbackPeriodMs!.Value;'),
 ('period: off by one', S+'Context.cs', 'return SwitchbackAssigner.FloorDiv(now - startAt, experiment.Data.SwitchbackPeriodMs!.Value);', 'return SwitchbackAssigner.FloorDiv(now - startAt, experiment.Data.SwitchbackPeriodMs!.Value) + 1;'),
 ('period: floordiv truncates', S+'Internal/SwitchbackAssigner.cs', 'return dividend % divisor != 0 && (dividend ^ divisor) < 0 ? q - 1 : q;', 'return q;'),
 ('period: slot uses truncating mod', S+'Internal/SwitchbackAssigner.cs', 'var cycleSlot = (int)unchecked(periodIndex - cycleIndex * armCount);', 'var cycleSlot = (int)System.Math.Abs(periodIndex % armCount);'),
 ('period: pre-anchor draws', S+'Context.cs', 'if (now < startAt) return null;', ''),
 ('period: anchor rounded instead of unservable', S+'Context.cs', 'start % SwitchbackAnchorAlignmentMs != 0)', 'false)'),
 ('period: negative anchor allowed', S+'Context.cs', 'if (startAt is not { } start || start < 0 ||', 'if (startAt is not { } start ||'),
 ('period: max anchor allowed', S+'Context.cs', 'start >= SwitchbackMaxAnchorMs ||', ''),
 ('period: zero period allowed', S+'Context.cs', 'if (periodMs is not > 0) return false;', 'if (periodMs is null) return false;'),
 ('period: one arm allowed', S+'Context.cs', 'return experiment.Split is { Length: >= 2 };', 'return experiment.Split is { Length: >= 1 };'),
 ('seed: sign-extended seedLo', S+'Context.cs', '((long)experiment.SeedHi << 32) | (experiment.SeedLo & 0xffffffffL);', '((long)experiment.SeedHi << 32) | (long)experiment.SeedLo;'),
 ('seed: halves swapped', S+'Internal/SwitchbackAssigner.cs', 'PutUInt32(buffer, 0, unchecked((int)seed));\n        PutUInt32(buffer, 4, unchecked((int)(seed >> 32)));', 'PutUInt32(buffer, 4, unchecked((int)seed));\n        PutUInt32(buffer, 0, unchecked((int)(seed >> 32)));'),
 ('utf8: Encoding.UTF8', S+'Internal/SwitchbackAssigner.cs', 'var keyLength = EncodeUtf8(keyBytes, key);', 'var keyLength = System.Text.Encoding.UTF8.GetBytes(key, 0, key.Length, keyBytes, 0);'),
 ('carryover: switchback exposure stamped at enqueue', S+'Context.cs', 'EnqueueExposure(assignment, assignment.Name, assignment.Switchback.ServingReadAt);', 'EnqueueExposure(assignment);'),
 ('carryover: retain 3 periods', S+'Context.cs', 'private const int SwitchbackRetainedPeriods = 2;', 'private const int SwitchbackRetainedPeriods = 3;'),
 ('carryover: evict just-installed period', S+'Context.cs', 'if (evicted == index) evicted = epoch.Periods.GetViewBetween(index + 1, long.MaxValue).Min;', ''),
 ('carryover: single slot per experiment', S+'Context.cs', 'periodIndex == null ? experimentName : SwitchbackCacheKey(experimentName, periodIndex.Value);', 'experimentName;'),
 ('carryover: serving re-read per slot rebuild', S+'Context.cs', 'return cached != null && cached.Serving.Status != SwitchbackServing.ServingStatus.Absent\n            ? (cached.Serving, cached.ServingReadAt)\n            : (ResolveSwitchbackServing(experiment, now), now);', 'return (ResolveSwitchbackServing(experiment, now), now);'),
 ('burn-in/out: burn fields become identity', S+'Context.cs', 'a.SwitchbackPeriodMs == b.SwitchbackPeriodMs &&', 'a.SwitchbackPeriodMs == b.SwitchbackPeriodMs && a.SwitchbackBurnInDurationMs == b.SwitchbackBurnInDurationMs && a.SwitchbackCarryoverDurationMs == b.SwitchbackCarryoverDurationMs && a.SwitchbackEstimand == b.SwitchbackEstimand &&'),
 ('burn-in/out: burn window suppresses exposure', S+'Context.cs', 'if (periodIndex is not { } period)\n        {', 'if (periodIndex is not { } period || data.SwitchbackBurnInDurationMs > 0 || data.SwitchbackCarryoverDurationMs > 0)\n        {'),
 ('epoch: generation always kept', S+'Context.cs', 'installed.SwitchbackGeneration = SameSwitchbackIdentity(previous, installed)', 'installed.SwitchbackGeneration = previous != null'),
 ('holdouts: apply to switchbacks', S+'Context.cs', '                experimentVariables.Holdouts = null;\n', ''),
 ('precedence: rules before ineligible', S+'Context.cs', '        assignment.Assigned = true;\n        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)', '        assignment.Assigned = true;\n        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible && experiment.AssignmentRules == null)'),
 ('precedence: split rule over experiment unit', S+'Context.cs', 'var rule = ResolveRule(experiment, serving.UnitType, false, true);', 'var rule = ResolveRule(experiment, data.UnitType, false, true);'),
 ('precedence: pre-anchor after traffic', S+'Context.cs', '        if (periodIndex is not { } period)\n        {\n            assignment.Eligible = false;\n            state.Silent = true;\n            return;\n        }\n', '', '        if (custom != null)\n        {\n            assignment.Variant = custom.Value;', '        if (periodIndex is not { } period) { assignment.Eligible = false; state.Silent = true; return; }\n        if (custom != null)\n        {\n            assignment.Variant = custom.Value;'),
 ('precedence: traffic miss silent', S+'Context.cs', '            // A traffic miss on an eligible cluster is recorded, as for a unit experiment.\n            assignment.Eligible = false;', '            // A traffic miss on an eligible cluster is recorded, as for a unit experiment.\n            assignment.Eligible = false; state.Silent = true;'),
 ('precedence: strict mismatch assigned', S+'Context.cs', '            // Recorded like a unit experiment\'s strict mismatch: control, audienceMismatch, not "assigned".\n            assignment.Assigned = false;', '            // Recorded like a unit experiment\'s strict mismatch: control, audienceMismatch, not "assigned".\n'),
 ('precedence: ineligible records', S+'Context.cs', '        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)\n        {\n            assignment.Eligible = false;\n            state.Silent = true;', '        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)\n        {\n            assignment.Eligible = false;'),
 ('allow-list: empty filters all', S+'Internal/SwitchbackServing.cs', 'if (allowedValues == null || allowedValues.Length == 0) return true;', 'if (allowedValues == null) return true;'),
 ('allow-list: case-insensitive', S+'Internal/SwitchbackServing.cs', 'if (string.Equals(key, allowed, StringComparison.Ordinal)) return true;', 'if (string.Equals(key, allowed, StringComparison.OrdinalIgnoreCase)) return true;'),
 ('disagreement ignored', S+'Internal/SwitchbackServing.cs', '                disagreement = true;', ''),
 ('legacy: unit type fallback draws', S+'Context.cs', 'if (experiment.SwitchbackSources == null) return SwitchbackServing.Legacy(experiment.Data.UnitType, _units);', 'if (experiment.SwitchbackSources == null) experiment.SwitchbackSources = new[] { new KeyValuePair<string, SwitchbackAttributePath>(experiment.Data.UnitType, SwitchbackAttributePath.ParseSupported("store_key")) };'),
 ('path: dot allowed', S+'Internal/SwitchbackAttributePath.cs', "if (path.IndexOf('.') >= 0) return null;", ''),
 ('path: latest attribute ignores setAt', S+'Context.cs', 'if (attribute.SetAt <= now && attribute.Name != null &&', 'if (attribute.Name != null &&'),
 ('variable: strict-mismatched switchback falls back to control', S+'Context.cs', 'return first.Switchback != null ? null : first;', 'return first;'),
 ('setUnit: absent slot not evicted', S+'Context.cs', 'if (Interlocked.CompareExchange(ref assignment.Exposed, 2, 0) == 0 || !assignment.Overridden)\n                    (evicted ??= new List<string>()).Add(entry.Key);', 'if (false) (evicted ??= new List<string>()).Add(entry.Key);'),
 ('custom: change not staleness', S+'Context.cs', 'if (assignment.Switchback != null) return Nullable.Equals(hasCustom ? custom : null, assignment.Switchback.CustomAssignment);', 'if (assignment.Switchback != null) return true;'),
 ('staleness: audience re-run for ineligible slot', S+'Context.cs', 'if (assignment.RuleOverride || assignment.Suppressed || !SwitchbackConsultedRules(assignment)) return true;', 'if (assignment.RuleOverride || assignment.Suppressed) return true;'),
 ('staleness: rules re-run for ineligible slot', S+'Context.cs', '        if (!SwitchbackConsultedRules(assignment)) return true;\n', ''),
 ('value rule: Half written as int', S+'Internal/SwitchbackAttributePath.cs', '        options.Converters.Add(new HalfAsFloatConverterFactory());\n', ''),
 ('value rule: enum keyed by number', S+'Internal/SwitchbackAttributePath.cs', '        options.Converters.Add(new EnumAsNullConverterFactory());\n', ''),
 ('wire: whole floats published as ints', S+'Context.cs', 'SwitchbackWireAttributes = _hasSwitchbacks,', 'SwitchbackWireAttributes = false,'),
 ('wire: wire form also without switchbacks', S+'Context.cs', 'SwitchbackWireAttributes = _hasSwitchbacks,', 'SwitchbackWireAttributes = true,'),
 ('wire: flag kept after refresh', S+'Context.cs', '_hasSwitchbacks = hasSwitchbacks;', '_hasSwitchbacks |= hasSwitchbacks;'),
 ('wire: flag only when all switchback', S+'Context.cs', '                hasSwitchbacks = true;', '                hasSwitchbacks = index.Count == 1;'),
 ('wire: setAt from publish clock', S+'Internal/SwitchbackAttributePath.cs', 'writer.WriteNumber(naming.ConvertName(nameof(Models.Attribute.SetAt)), value.SetAt);', 'writer.WriteNumber(naming.ConvertName(nameof(Models.Attribute.SetAt)), 0);'),
 ('wire: float widened to double text', S+'Internal/SwitchbackAttributePath.cs', '            v => ((float)v).ToString("R", CultureInfo.InvariantCulture)));', '            null));'),
 ('wire: double-keyed dictionaries written as 3.0', S+'Internal/SwitchbackAttributePath.cs', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            Default(options).WriteAsPropertyName(writer, value, options);', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            writer.WritePropertyName(_toText(value!) + ".0");'),
 ('wire: published values replaced', S+'Context.cs', 'Attributes = _attributes.Count == 0 ? null : _attributes.ToArray(),', 'Attributes = _attributes.Count == 0 ? null : _attributes.Select(a => new Attribute { Name = a.Name, Value = a.Value is double d ? (object)(float)d : a.Value, SetAt = a.SetAt }).ToArray(),'),
 ('stamp: exposing call instant', S+'Context.cs', 'EnqueueExposure(assignment, assignment.Name, assignment.Switchback.ServingReadAt);', 'EnqueueExposure(assignment, assignment.Name, _clock.Millis());'),
 ('enum: dictionary keys rejected', S+'Internal/SwitchbackAttributePath.cs', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            ((JsonConverter<T>)', '        public void WriteAsPropertyName2(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            ((JsonConverter<T>)'),
 ('randomization type case-insensitive', S+'Context.cs', 'string.Equals(experiment.RandomizationType, RandomizationSwitchback, StringComparison.Ordinal);', 'string.Equals(experiment.RandomizationType, RandomizationSwitchback, StringComparison.OrdinalIgnoreCase);'),
]
EQUIVALENT = {
    # now >= startAt, so the dividend is never negative and truncation equals floorDiv.
    'period: truncating division',
    # NaN/Infinity: both the wire and the default converter reject them; the lookup gives no value.
}
only = sys.argv[1:]
results = []
for name, path, old, new, *more in M:
    if only and not any(o in name for o in only): continue
    p = pathlib.Path(path); src = p.read_text()
    if src.count(old) != 1:
        results.append((name, 'NOT APPLIED (%d matches)' % src.count(old))); continue
    mutated = src.replace(old, new)
    if more:
        if mutated.count(more[0]) != 1:
            results.append((name, 'NOT APPLIED (second edit)')); continue
        mutated = mutated.replace(more[0], more[1])
    p.write_text(mutated)
    try:
        r = subprocess.run(['dotnet', 'test', 'tests/ABSmartly.Sdk.Tests', '-f', 'net9.0'],
                           env=env, capture_output=True, text=True, timeout=900)
        out = r.stdout + r.stderr
        if 'error CS' in out: status = 'killed (compile error)'
        elif r.returncode != 0:
            failed = [l.strip() for l in out.splitlines() if l.strip().startswith('Failed ')][:3]
            status = 'killed: ' + '; '.join(failed)
        else: status = 'SURVIVED'
    finally:
        p.write_text(src)
    results.append((name, status)); print(name, '->', status, flush=True)
print()
for n, s in results:
    tag = 'OK  ' if s.startswith('killed') else ('EQ  ' if n in EQUIVALENT and s == 'SURVIVED' else '!!  ')
    print(tag + n + ': ' + s)
sys.exit(1 if any(not s.startswith('killed') and n not in EQUIVALENT for n, s in results) else 0)
