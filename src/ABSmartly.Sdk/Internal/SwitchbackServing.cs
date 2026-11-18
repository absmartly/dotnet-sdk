using System;
using System.Collections.Generic;

namespace ABSmartly.Internal;

// Which unit serves a switchback experiment and under which cluster key. Port of the Collector's
// ContextAPI.switchbackServing (R33, cluster-defined switchbacks), via java-sdk SwitchbackServing:
//
// - The experiment's switchbackClusters, in their declared order (unit_type_id ascending), list
//   the linked unit types and, for each, the attribute path holding its cluster value.
// - No context unit of a linked type: the experiment does not apply (Absent).
// - Otherwise each linked unit, in source order, reads the attribute at its source's path,
//   normalised by SwitchbackClusterKeys. The first unit yielding a value is the serving unit and the
//   value is the cluster key; units yielding no value are skipped.
// - Ineligible, served under the first linked unit: no unit yields a value, two units yield
//   different values, or an allow-list is set and lacks the key.
// - Eligible otherwise.
// - A missing, empty or unreadable cluster map takes the Collector's legacy path instead (Legacy).
internal sealed class SwitchbackServing
{
    public enum ServingStatus
    {
        Absent,
        Ineligible,
        Eligible
    }

    private static readonly SwitchbackServing AbsentServing = new(ServingStatus.Absent, null, null);

    public ServingStatus Status { get; }
    // The serving unit's type when Eligible, the first linked unit's when Ineligible, null when Absent.
    public string UnitType { get; }
    // The raw normalised cluster key when Eligible; never hashed.
    public string ClusterKey { get; }

    private SwitchbackServing(ServingStatus status, string unitType, string clusterKey)
    {
        Status = status;
        UnitType = unitType;
        ClusterKey = clusterKey;
    }

    // sources is a readable cluster map (see Sources); an unreadable one takes Legacy instead. The
    // declared source order alone decides which unit serves: the context keeps one uid per unit type,
    // so each source has at most one unit and the source loop is the only order there is.
    public static SwitchbackServing Resolve(KeyValuePair<string, SwitchbackAttributePath>[] sources,
        string[] allowedValues, IReadOnlyDictionary<string, string> units, Func<SwitchbackAttributePath, object> read)
    {
        string first = null;
        string serving = null;
        string key = null;
        var disagreement = false;
        foreach (var source in sources)
        {
            if (!units.ContainsKey(source.Key)) continue;

            first ??= source.Key;
            var value = SwitchbackClusterKeys.Normalize(read(source.Value));
            if (value == null) continue;

            if (key == null)
            {
                key = value;
                serving = source.Key;
            }
            else if (!string.Equals(key, value, StringComparison.Ordinal))
            {
                disagreement = true;
            }
        }

        if (first == null) return AbsentServing;
        if (key == null || disagreement || !Allowed(allowedValues, key))
            return new SwitchbackServing(ServingStatus.Ineligible, first, null);
        return new SwitchbackServing(ServingStatus.Eligible, serving, key);
    }

    // The Collector's compatibility path for a missing, empty or unreadable cluster map: the
    // experiment's declared unitType only identifies an ineligible control response (never an
    // assignment or a cluster); without a unit of that type the experiment does not apply.
    public static SwitchbackServing Legacy(string unitType, IReadOnlyDictionary<string, string> units) =>
        unitType != null && units.ContainsKey(unitType)
            ? new SwitchbackServing(ServingStatus.Ineligible, unitType, null)
            : AbsentServing;

    // A null or empty allow-list admits every value: the Collector serves an experiment that has no
    // allow-list rows with this field absent, and builds its filter only when the list is non-empty.
    internal static bool Allowed(string[] allowedValues, string key)
    {
        if (allowedValues == null || allowedValues.Length == 0) return true;
        foreach (var allowed in allowedValues)
        {
            if (string.Equals(key, allowed, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    // The linked unit types and their parsed paths, in declared order; null for a missing or empty
    // map, a null unit type, or any path that is not a supported attribute path (the legacy path).
    public static KeyValuePair<string, SwitchbackAttributePath>[] Sources(
        IReadOnlyList<KeyValuePair<string, string>> clusters)
    {
        if (clusters == null || clusters.Count == 0) return null;

        var sources = new KeyValuePair<string, SwitchbackAttributePath>[clusters.Count];
        for (var i = 0; i < clusters.Count; i++)
        {
            var path = clusters[i].Key == null ? null : SwitchbackAttributePath.ParseSupported(clusters[i].Value);
            if (path == null) return null;
            sources[i] = new KeyValuePair<string, SwitchbackAttributePath>(clusters[i].Key, path);
        }

        return sources;
    }
}
