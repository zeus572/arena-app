namespace Civic.API.Services.Coalition.Geometry;

/// <summary>One observed (version, accept?) signal for a player, optionally timestamped.</summary>
public sealed record AcceptanceSignal(VersionPoint Version, bool Accept, DateTime? At = null);

/// <summary>
/// Derives a player's <see cref="AcceptanceRegion"/> in sub-question space from
/// their sparse accept/decline signals over extracted version vectors (the inputs
/// named by the plan: AcceptanceRecords + extracted vectors). Pure computation.
///
/// Model (recorded assumption): a player's acceptable label set for a sub-question
/// is the UNION of the labels they have co-signed on that sub-question. A version
/// that resolves a constrained sub-question to a label the player has never signed
/// is treated as outside their region.
///
/// Limitation (by design, per doc 06): a DECLINE cannot be pinned to a specific
/// sub-question from sparse data alone (the decline may be driven by any one of the
/// version's positions), so declines are not used to subtract labels here — precise
/// acceptance-set inference is done by PROBING at near-coalition time, which is a
/// later layer. Declines ARE used for movement detection (see MovementDetector).
/// </summary>
public static class AcceptanceSetDeriver
{
    public static AcceptanceRegion Derive(IEnumerable<AcceptanceSignal> signals)
    {
        var acceptableByKey = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in signals.Where(s => s.Accept))
        {
            foreach (var (key, label) in s.Version.Positions)
            {
                if (!acceptableByKey.TryGetValue(key, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    acceptableByKey[key] = set;
                }
                set.Add(label);
            }
        }
        return new AcceptanceRegion(
            acceptableByKey.ToDictionary(kv => kv.Key, kv => (IEnumerable<string>)kv.Value));
    }
}
