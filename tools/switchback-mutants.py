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
 ('period: off by one', S+'Context.cs', 'return (now - startAt) / experiment.Data.SwitchbackPeriodMs!.Value;', 'return (now - startAt) / experiment.Data.SwitchbackPeriodMs!.Value + 1;'),
 ('period: floordiv truncates', S+'Internal/SwitchbackAssigner.cs', 'return dividend % divisor != 0 && (dividend ^ divisor) < 0 ? q - 1 : q;', 'return q;'),
 ('period: slot uses truncating mod', S+'Internal/SwitchbackAssigner.cs', 'var cycleSlot = (int)unchecked(periodIndex - cycleIndex * armCount);', 'var cycleSlot = (int)System.Math.Abs(periodIndex % armCount);'),
 ('period: pre-anchor draws', S+'Context.cs', 'if (now < startAt) return null;', ''),
 ('period: anchor rounded instead of unservable', S+'Context.cs', 'start % SwitchbackAnchorAlignmentMs != 0)', 'false)'),
 ('period: negative anchor allowed', S+'Context.cs', 'if (startAt is not { } start || start < 0 ||', 'if (startAt is not { } start ||'),
 ('period: max anchor allowed', S+'Context.cs', 'start >= SwitchbackMaxAnchorMs ||', ''),
 ('period: zero period allowed', S+'Context.cs', 'if (periodMs is not > 0) return false;', 'if (periodMs is null) return false;'),
 ('period: one arm allowed', S+'Context.cs', 'return experiment.Split is { Length: >= 2 };', 'return experiment.Split is { Length: >= 1 };'),
 ('seed: sign-extended seedLo', S+'Context.cs', '((long)experiment.SeedHi << 32) | (experiment.SeedLo & 0xffffffffL);', '((long)experiment.SeedHi << 32) | (long)experiment.SeedLo;'),
 ('seed: halves swapped', S+'Internal/SwitchbackAssigner.cs', 'BinaryPrimitives.WriteInt64LittleEndian(buffer, seed);', 'BinaryPrimitives.WriteInt64LittleEndian(buffer, (seed << 32) | (long)((ulong)seed >> 32));'),
 ('seed: big-endian packing', S+'Internal/SwitchbackAssigner.cs', 'BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), i);', 'BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(4), i);'),
 ('utf8: Encoding.UTF8', S+'Internal/SwitchbackAssigner.cs', 'var keyLength = EncodeUtf8(keyBytes, key);', 'var keyLength = System.Text.Encoding.UTF8.GetBytes(key, 0, key.Length, keyBytes, 0);'),
 ('carryover: switchback exposure stamped at enqueue', S+'Context.cs', 'EnqueueExposure(assignment, assignment.Name, assignment.Switchback.ServingReadAt,', 'EnqueueExposure(assignment, assignment.Name, _clock.Millis(),'),
 ('carryover: retain 3 periods', S+'Context.cs', 'private const int SwitchbackRetainedPeriods = 2;', 'private const int SwitchbackRetainedPeriods = 3;'),
 ('carryover: evict just-installed period', S+'Context.cs', 'if (evicted == index) evicted = epoch.Periods.GetViewBetween(index + 1, long.MaxValue).Min;', ''),
 ('carryover: single slot per experiment', S+'Context.cs', 'periodIndex == null ? experimentName : SwitchbackCacheKey(experimentName, periodIndex.Value);', 'experimentName;'),
 ('carryover: serving re-read per slot rebuild', S+'Context.cs', 'return cached != null && cached.Serving.Status != SwitchbackServing.ServingStatus.Absent\n            ? (cached.Serving, cached.ServingReadAt, cached.ServingUnitCount)\n            : (ResolveSwitchbackServing(experiment, now), now, _unitOrder.Count);', 'return (ResolveSwitchbackServing(experiment, now), now, _unitOrder.Count);'),
 ('burn-in/out: burn fields become identity', S+'Context.cs', 'a.SwitchbackPeriodMs == b.SwitchbackPeriodMs &&', 'a.SwitchbackPeriodMs == b.SwitchbackPeriodMs && a.SwitchbackBurnInDurationMs == b.SwitchbackBurnInDurationMs && a.SwitchbackCarryoverDurationMs == b.SwitchbackCarryoverDurationMs && a.SwitchbackEstimand == b.SwitchbackEstimand &&'),
 ('burn-in/out: burn window suppresses exposure', S+'Context.cs', 'if (periodIndex is not { } period)\n        {', 'if (periodIndex is not { } period || data.SwitchbackBurnInDurationMs > 0 || data.SwitchbackCarryoverDurationMs > 0)\n        {'),
 ('epoch: generation always kept', S+'Context.cs', 'installed.SwitchbackGeneration = SameSwitchbackIdentity(previous, installed)', 'installed.SwitchbackGeneration = previous != null'),
 ('holdouts: apply to switchbacks', S+'Context.cs', 'Holdouts = IsSwitchback(experiment) ? null : ResolveHoldouts(experiment.HoldoutIds, holdoutsById)', 'Holdouts = ResolveHoldouts(experiment.HoldoutIds, holdoutsById)'),
 ('precedence: rules before ineligible', S+'Context.cs', '        assignment.Assigned = true;\n        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)', '        assignment.Assigned = true;\n        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible && experiment.AssignmentRules == null)'),
 ('precedence: split rule over experiment unit', S+'Context.cs', 'var rule = ResolveRule(experiment, serving.UnitType, false, true);', 'var rule = ResolveRule(experiment, data.UnitType, false, true);'),
 ('precedence: pre-anchor after traffic', S+'Context.cs', '        if (periodIndex is not { } period)\n        {\n            SilentControl(assignment, state);\n            return;\n        }\n', '', '        if (custom != null)\n        {\n            assignment.Variant = custom.Value;', '        if (periodIndex is not { } period) { assignment.Eligible = false; state.Silent = true; return; }\n        if (custom != null)\n        {\n            assignment.Variant = custom.Value;'),
 ('precedence: traffic miss silent', S+'Context.cs', '            assignment.Eligible = false;\n            return;\n        }\n\n        if (custom != null)', '            assignment.Eligible = false; state.Silent = true;\n            return;\n        }\n\n        if (custom != null)'),
 ('precedence: strict mismatch assigned', S+'Context.cs', '            // Recorded like a unit experiment\'s strict mismatch: control, audienceMismatch, not "assigned".\n            assignment.Assigned = false;', '            // Recorded like a unit experiment\'s strict mismatch: control, audienceMismatch, not "assigned".\n'),
 ('precedence: ineligible records', S+'Context.cs', '        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)\n        {\n            SilentControl(assignment, state);', '        if (serving.Status == SwitchbackServing.ServingStatus.Ineligible)\n        {\n            assignment.Eligible = false;'),
 ('allow-list: empty filters all', S+'Internal/SwitchbackServing.cs', 'if (allowedValues == null || allowedValues.Length == 0) return true;', 'if (allowedValues == null) return true;'),
 ('allow-list: case-insensitive', S+'Internal/SwitchbackServing.cs', 'if (string.Equals(key, allowed, StringComparison.Ordinal)) return true;', 'if (string.Equals(key, allowed, StringComparison.OrdinalIgnoreCase)) return true;'),
 ('disagreement ignored', S+'Internal/SwitchbackServing.cs', '                disagreement = true;', ''),
 ('legacy: unit type fallback draws', S+'Context.cs', 'if (experiment.SwitchbackSources == null) return SwitchbackServing.Legacy(experiment.Data.UnitType, _units);', 'if (experiment.SwitchbackSources == null) experiment.SwitchbackSources = new[] { new KeyValuePair<string, SwitchbackAttributePath>(experiment.Data.UnitType, SwitchbackAttributePath.ParseSupported("store_key")) };'),
 ('path: dot allowed', S+'Internal/SwitchbackAttributePath.cs', "if (path.IndexOf('.') >= 0) return null;", ''),
 ('path: latest attribute ignores setAt', S+'Context.cs', 'if (attribute.SetAt <= now && attribute.Name != null &&', 'if (attribute.Name != null &&'),
 ('setUnit: absent slot not evicted', S+'Context.cs', 'if (Interlocked.CompareExchange(ref assignment.Exposed, 2, 0) == 0 || !assignment.Overridden)\n                    (evicted ??= new List<string>()).Add(entry.Key);', 'if (false) (evicted ??= new List<string>()).Add(entry.Key);'),
 ('custom: change not staleness', S+'Context.cs', 'if (assignment.Switchback != null) return Nullable.Equals(hasCustom ? custom : null, assignment.Switchback.CustomAssignment);', 'if (assignment.Switchback != null) return true;'),
 ('staleness: audience re-run for ineligible slot', S+'Context.cs', 'if (assignment.RuleOverride || assignment.Suppressed || !SwitchbackConsultedRules(assignment)) return true;', 'if (assignment.RuleOverride || assignment.Suppressed) return true;'),
 ('staleness: rules re-run for ineligible slot', S+'Context.cs', '        if (!SwitchbackConsultedRules(assignment)) return true;\n', ''),
 ('value rule: Half written as int', S+'Internal/SwitchbackAttributePath.cs', '        options.Converters.Add(new HalfAsFloatConverterFactory());\n', ''),
 ('value rule: enum keyed by number', S+'Internal/SwitchbackAttributePath.cs', '        options.Converters.Add(new EnumAsNameConverterFactory());\n', ''),
 ('wire: enum name lowercased', S+'Internal/SwitchbackAttributePath.cs', 'writer.WriteStringValue(value!.ToString());', 'writer.WriteStringValue(value!.ToString().ToLowerInvariant());'),
 ('wire: NaN through the default converter', S+'Internal/SwitchbackAttributePath.cs', '                text == numberFormat.NegativeInfinitySymbol)\n            {\n                writer.WriteNullValue();', '                text == numberFormat.NegativeInfinitySymbol)\n            {\n                writer.WriteRawValue(text, skipInputValidation: true);'),
 ('publish: publishedAt ignores exposures', S+'Context.cs', 'if (exposure.ExposedAt > publishedAt) publishedAt = exposure.ExposedAt;', 'if (exposure.ExposedAt < 0) publishedAt = exposure.ExposedAt;'),
 ('publish: publishedAt always max of exposures', S+'Context.cs', '        var publishedAt = _clock.Millis();\n        if (!_hasSwitchbacks) return publishedAt;', '        var publishedAt = 0L;\n        if (!_hasSwitchbacks) return _clock.Millis();'),
 ('wire: whole floats published as ints', S+'Context.cs', 'SwitchbackWireAttributes = _hasSwitchbacks,', 'SwitchbackWireAttributes = false,'),
 ('wire: wire form also without switchbacks', S+'Context.cs', 'SwitchbackWireAttributes = _hasSwitchbacks,', 'SwitchbackWireAttributes = true,'),
 ('wire: flag only when all switchback', S+'Context.cs', '                hasSwitchbacks = true;', '                hasSwitchbacks = index.Count == 1;'),
 ('wire: setAt from publish clock', S+'Internal/SwitchbackAttributePath.cs', 'writer.WriteNumber(naming.ConvertName(nameof(Models.Attribute.SetAt)), value.SetAt);', 'writer.WriteNumber(naming.ConvertName(nameof(Models.Attribute.SetAt)), 0);'),
 ('wire: float widened to double text', S+'Internal/SwitchbackAttributePath.cs', 'var text = value.ToString("R", CultureInfo.InvariantCulture);', 'var text = Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);'),
 ('wire: double-keyed dictionaries written as 3.0', S+'Internal/SwitchbackAttributePath.cs', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            Default(options).WriteAsPropertyName(writer, value, options);', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            writer.WritePropertyName(value.ToString("R", CultureInfo.InvariantCulture) + ".0");'),
 ('wire: published values replaced', S+'Context.cs', 'Attributes = _attributes.Count == 0 ? null : _attributes.ToArray(),', 'Attributes = _attributes.Count == 0 ? null : _attributes.Select(a => new Attribute { Name = a.Name, Value = a.Value is double d ? (object)(float)d : a.Value, SetAt = a.SetAt }).ToArray(),'),
 ('enum: dictionary keys rejected', S+'Internal/SwitchbackAttributePath.cs', '        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            ((JsonConverter<T>)', '        public void WriteAsPropertyName2(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>\n            ((JsonConverter<T>)'),
 ('wire: Half text via double', S+'Internal/SwitchbackAttributePath.cs', 'var text = value.ToString("R", CultureInfo.InvariantCulture);', 'var text = typeof(T).FullName == "System.Half" ? Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture) : value.ToString("R", CultureInfo.InvariantCulture);'),
 ('wire: goals take the wire form', S+'Internal/SwitchbackAttributePath.cs', '        var options = new JsonSerializerOptions(JsonOptionsProvider.Default.SerializerOptions);\n        options.Converters.Add(new WireAttributeConverter());', '        var options = new JsonSerializerOptions(WireOptions.Value);\n        options.Converters.Add(new WireAttributeConverter());'),
 ('wire: null name written', S+'Internal/SwitchbackAttributePath.cs', 'if (value.Name != null) writer.WriteString(', 'writer.WriteString('),
 ('wire: null value written', S+'Internal/SwitchbackAttributePath.cs', '            if (value.Value != null)\n            {', '            if (true)\n            {'),
 ('wire: flag cleared by refresh', S+'Context.cs', 'if (hasSwitchbacks) _hasSwitchbacks = true;', '_hasSwitchbacks = hasSwitchbacks;'),
 ('rules: revalidate over payload unitType', S+'Context.cs', 'assignment.Switchback != null ? assignment.UnitType : experiment.Data.UnitType', 'experiment.Data.UnitType'),
 ('allow-list: numbers skipped', S+'Services/Json/SwitchbackConverters.cs', '                case JsonTokenType.Number:\n', '                case JsonTokenType.Comment:\n'),
 ('allow-list: rejected elements dropped', S+'Services/Json/SwitchbackConverters.cs', '                    reader.Skip();\n                    values.Add(null);', '                    reader.Skip();'),
 ('allow-list: null entry admits all', S+'Internal/SwitchbackServing.cs', 'if (string.Equals(key, allowed, StringComparison.Ordinal)) return true;', 'if (allowed == null || string.Equals(key, allowed, StringComparison.Ordinal)) return true;'),
 ('publish: clamp without switchbacks', S+'Context.cs', '        if (!_hasSwitchbacks) return publishedAt;', ''),
 ('publish: clamp past the exposure', S+'Context.cs', 'if (exposure.ExposedAt > publishedAt) publishedAt = exposure.ExposedAt;', 'if (exposure.ExposedAt > publishedAt) publishedAt = exposure.ExposedAt + 1;'),
 ('publish: clamp to the first exposure only', S+'Context.cs', 'if (exposure.ExposedAt > publishedAt) publishedAt = exposure.ExposedAt;', 'if (exposure == exposures[0] && exposure.ExposedAt > publishedAt) publishedAt = exposure.ExposedAt;'),
 ('publish: no clamp for multi-exposure batches', S+'Context.cs', '        if (!_hasSwitchbacks) return publishedAt;', '        if (!_hasSwitchbacks || exposures is { Length: > 1 }) return publishedAt;'),
 ('variable: control fallback restored', S+'Context.cs', '        failure?.Throw();\n        return suppressedFallback;\n    }', '        failure?.Throw();\n        if (suppressedFallback != null) return suppressedFallback;\n        return experiments.Count > 0 ? GetAssignment(experiments[0].Data.Name, ref now) : null;\n    }'),
 ('variable: unassigned non-strict control fallback', S+'Context.cs', '        failure?.Throw();\n        return suppressedFallback;\n    }', '        failure?.Throw();\n        if (suppressedFallback != null) return suppressedFallback;\n        if (experiments.Count == 0) return null;\n        var first = GetAssignment(experiments[0].Data.Name, ref now);\n        return first.Switchback != null || first.AudienceMismatch ? null : first;\n    }'),
 ('replay: rewound write keeps clock reading', S+'Context.cs', '_clusterRootNames.Contains(SwitchbackAttributePath.JavaTrim(name))) setAt = latestRead + 1;', 'false) setAt = latestRead + 1;'),
 ('replay: rewound write dated at the read', S+'Context.cs', '_clusterRootNames.Contains(SwitchbackAttributePath.JavaTrim(name))) setAt = latestRead + 1;', '_clusterRootNames.Contains(SwitchbackAttributePath.JavaTrim(name))) setAt = latestRead;'),
 ('replay: same-ms write bumped too', S+'Context.cs', 'setAt < latestRead && name != null &&', 'setAt <= latestRead && name != null &&'),
 ('replay: latest read not tracked', S+'Context.cs', '        if (servingReadAt > _latestServingReadAt) _latestServingReadAt = servingReadAt;', ''),
 ('replay: latest read is the last read', S+'Context.cs', '        if (servingReadAt > _latestServingReadAt) _latestServingReadAt', '        if (true) _latestServingReadAt'),
 ('units: no split for late units', S+'Context.cs', 'if (exposures == null || !exposures.Any(e => HasLateLinkedUnit(e, units)))', 'if (true)'),
 ('units: serving count not recorded', S+'Context.cs', '            : (ResolveSwitchbackServing(experiment, now), now, _unitOrder.Count);', '            : (ResolveSwitchbackServing(experiment, now), now, 0);'),
 ('units: split request sends every unit', S+'Context.cs', 'Units = request.UnitCount < 0 ? units : units.Take(request.UnitCount).ToArray(),', 'Units = units,'),
 ('units: failure restores delivered requests', S+'Context.cs', 'var pending = requests?.Skip(delivered.Count).ToArray();', 'var pending = requests;'),
 ('units: unlinked late unit splits', S+'Context.cs', 'if (Array.IndexOf(exposure.LinkedUnitTypes, units[i].Type) >= 0) return true;', 'return true;'),
 ('units: split groups descending', S+'Context.cs', '.GroupBy(e => e.ServingUnitCount).OrderBy(g => g.Key))', '.GroupBy(e => e.ServingUnitCount).OrderByDescending(g => g.Key))'),
 ('units: earlier requests not logged', S+'Context.cs', '        foreach (var published in outcome.Delivered) LogEventSafely(EventType.Publish, published);', '        LogEventSafely(EventType.Publish, outcome.Delivered[outcome.Delivered.Length - 1]);', '        if (outcome.Delivered == null) return;', '        if (outcome.Delivered is not { Length: > 0 }) return;'),
 ('units: earlier requests not logged on close', S+'Context.cs', '            LogPublishes(earlier);\n', ''),
 ('units: failure loses delivered from log', S+'Context.cs', 'return new FlushOutcome(delivered.ToArray(), e);', 'return new FlushOutcome(null, e);'),
 ('replay: every attribute re-dated', S+'Context.cs', '_clusterRootNames.Contains(SwitchbackAttributePath.JavaTrim(name))) setAt = latestRead + 1;', 'true) setAt = latestRead + 1;'),
 ('replay: cluster roots not recorded', S+'Context.cs', 'try { _clusterRootNames.UnionWith(clusterRoots); }', 'try { clusterRoots.Clear(); }'),
 ('concurrency: live units per split request', S+'Context.cs', 'Units = request.UnitCount < 0 ? units : units.Take(request.UnitCount).ToArray(),', 'Units = (request.UnitCount < 0 ? _unitOrder : _unitOrder.Take(request.UnitCount)).Select(t => new Unit { Type = t, Uid = Encoding.ASCII.GetString(GetUnitHash(t, _units[t])) }).ToArray(),'),
 ('concurrency: roots registered under the data lock', S+'Context.cs', '        if (clusterRoots != null)\n        {\n            _contextLock.EnterWriteLock();\n            try { _clusterRootNames.UnionWith(clusterRoots); }\n            finally { _contextLock.ExitWriteLock(); }\n        }\n', '', '            if (hasSwitchbacks) _hasSwitchbacks = true;\n', '            if (hasSwitchbacks) _hasSwitchbacks = true;\n            if (clusterRoots != null) { _contextLock.EnterWriteLock(); try { _clusterRootNames.UnionWith(clusterRoots); } finally { _contextLock.ExitWriteLock(); } }\n'),
 ('concurrency: append outside the write lock', S+'Context.cs', '            if (InsideAttributeAppend != null) Interlocked.Exchange(ref InsideAttributeAppend, null)?.Invoke();\n            _attributes.Add(new Attribute { Name = name, Value = value, SetAt = setAt });\n            Interlocked.Increment(ref _attrsSeq);\n        }\n        finally { _contextLock.ExitWriteLock(); }', '            _contextLock.ExitWriteLock();\n            if (InsideAttributeAppend != null) Interlocked.Exchange(ref InsideAttributeAppend, null)?.Invoke();\n            _attributes.ConcurrentAdd(new Attribute { Name = name, Value = value, SetAt = setAt });\n            Interlocked.Increment(ref _attrsSeq);\n        }\n        finally { }'),
 ('randomization type case-insensitive', S+'Context.cs', 'string.Equals(experiment.RandomizationType, RandomizationSwitchback, StringComparison.Ordinal);', 'string.Equals(experiment.RandomizationType, RandomizationSwitchback, StringComparison.OrdinalIgnoreCase);'),
 ('replay: split request publishedAt per request', S+'Context.cs', 'PublishedAt = publishedAt,', 'PublishedAt = PublishedAt(request.Exposures, request.Goals),'),
 ('replay: lookup trims as .NET', S+'Context.cs', 'string.Equals(name, SwitchbackAttributePath.JavaTrim(attribute.Name), StringComparison.Ordinal)', 'string.Equals(name, attribute.Name.Trim(), StringComparison.Ordinal)'),
 ('replay: rewind trims as .NET', S+'Context.cs', '_clusterRootNames.Contains(SwitchbackAttributePath.JavaTrim(name))', '_clusterRootNames.Contains(name.Trim())'),
 ('replay: java trim keeps U+0020', S+'Internal/SwitchbackAttributePath.cs', "while (start < end && s[start] <= ' ') start++;", "while (start < end && s[start] < ' ') start++;"),
 ('replay: batch publishedAt from the first request', S+'Context.cs', 'var publishedAt = PublishedAt(exposures, achievements);', 'requests = SplitByServingUnits(exposures, achievements, units);\n                var publishedAt = PublishedAt(requests[0].Exposures, requests[0].Goals);'),
 ('replay: java trim end guard', S+'Internal/SwitchbackAttributePath.cs', "while (end > start && s[end - 1] <= ' ') end--;", "while (end > 0 && s[end - 1] <= ' ') end--;"),
 ('replay: java trim strips leading U+00A0', S+'Internal/SwitchbackAttributePath.cs', "while (start < end && s[start] <= ' ') start++;", "while (start < end && (s[start] <= ' ' || s[start] == '\\u00a0')) start++;"),
 ('replay: java trim then .NET TrimEnd', S+'Internal/SwitchbackAttributePath.cs', "return start == 0 && end == s.Length ? s : s.Substring(start, end - start);", "return (start == 0 && end == s.Length ? s : s.Substring(start, end - start)).TrimEnd();"),
 ('cache: mid-period attribute re-reads the cluster', S+'Context.cs', '        if (assignment.RuleOverride || assignment.Suppressed || !SwitchbackConsultedRules(assignment)) return true;\n\n        if (!string.IsNullOrEmpty(experiment.Audience) && attrsSeq > assignment.AttrsSeq)', '        if (assignment.Switchback != null && attrsSeq > assignment.AttrsSeq) return false;\n        if (assignment.RuleOverride || assignment.Suppressed || !SwitchbackConsultedRules(assignment)) return true;\n\n        if (!string.IsNullOrEmpty(experiment.Audience) && attrsSeq > assignment.AttrsSeq)', 'return cached != null && cached.Serving.Status != SwitchbackServing.ServingStatus.Absent\n            ? (cached.Serving, cached.ServingReadAt, cached.ServingUnitCount)\n            : (ResolveSwitchbackServing(experiment, now), now, _unitOrder.Count);', 'return (ResolveSwitchbackServing(experiment, now), now, _unitOrder.Count);'),
 ('recovery: failure before the first request drops the batch', S+'Context.cs', 'var pending = requests?.Skip(delivered.Count).ToArray();', 'var pending = requests?.Skip(delivered.Count).ToArray() ?? Array.Empty<PublishRequest>();'),
 ('recovery: preparation outside the restoring handler', S+'Context.cs', '                var publishedAt = PublishedAt(exposures, achievements);\n', '', '        PublishRequest[] requests = null;\n', '        PublishRequest[] requests = null;\n        var publishedAt = PublishedAt(exposures, achievements);\n'),
 ('recovery: retried goal ignored by publishedAt', S+'Context.cs', '                if (goal.AchievedAt > publishedAt) publishedAt = goal.AchievedAt;', '                { }'),
 ('recovery: goals bound publishedAt without switchbacks', S+'Context.cs', '        var publishedAt = _clock.Millis();\n        if (!_hasSwitchbacks) return publishedAt;', '        var publishedAt = _clock.Millis();'),
 ('recovery: mixed batch ignores goals', S+'Context.cs', 'if (goals != null)', 'if (goals != null && exposures == null)'),
 ('recovery: only first pending exposure group restored', S+'Context.cs', 'pending.SelectMany(r => r.Exposures', 'pending.Take(1).SelectMany(r => r.Exposures'),
 ('wire: negative infinity not null', S+'Internal/SwitchbackAttributePath.cs', 'text == numberFormat.NaNSymbol || text == numberFormat.PositiveInfinitySymbol ||\n                text == numberFormat.NegativeInfinitySymbol', 'text == numberFormat.NaNSymbol || text == numberFormat.PositiveInfinitySymbol'),
 ('cache: cluster paths re-parsed per lookup', S+'Context.cs', '        if (experiment == null || !IsSwitchback(experiment.Data) || experiment.SwitchbackSources == null ||', '        if (experiment != null && IsSwitchback(experiment.Data)) experiment.SwitchbackSources = SwitchbackServing.Sources(experiment.Data.SwitchbackClusters);\n        if (experiment == null || !IsSwitchback(experiment.Data) || experiment.SwitchbackSources == null ||'),
 ('recovery: goal bound off by one', S+'Context.cs', 'if (goal.AchievedAt > publishedAt) publishedAt = goal.AchievedAt;', 'if (goal.AchievedAt > publishedAt + 1) publishedAt = goal.AchievedAt;'),
 ('units: close logs the final flush first', S+'Context.cs', '            LogPublishes(earlier);\n            ReportFlush(outcome);', '            LogPublishes(outcome);\n            LogPublishes(earlier);\n            ReportFlush(new FlushOutcome(null, outcome.Error));'),
]
EQUIVALENT = {
    'wire: Half text via double',
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
