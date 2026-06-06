namespace Civic.API.Services.Coalition.Geometry;

/// <summary>
/// Phase 1.1 — acceptance-set overlap. Pure computation (no LLM).
///
/// Overlap of acceptance sets = intersection of acceptable regions in
/// sub-question space (doc 06). A coalition exists where enough acceptance
/// regions intersect on a specific configuration that also spans the spectrum —
/// breadth/spanning is Phase 1.2+; this phase answers the membership question:
/// does a given version sit inside all of a set of players' regions?
/// </summary>
public static class OverlapCalculator
{
    /// <summary>overlap(players, version): does this version sit inside ALL players' regions?</summary>
    public static bool Overlaps(IEnumerable<PlayerGeometry> players, VersionPoint version)
        => players.All(p => p.Region.Contains(version));

    /// <summary>The subset of players whose region contains the version (its supporters).</summary>
    public static IReadOnlyList<PlayerGeometry> Supporters(IEnumerable<PlayerGeometry> players, VersionPoint version)
        => players.Where(p => p.Region.Contains(version)).ToList();

    /// <summary>How many of the given players would co-sign this version.</summary>
    public static int SupportCount(IEnumerable<PlayerGeometry> players, VersionPoint version)
        => players.Count(p => p.Region.Contains(version));

    /// <summary>
    /// The intersection region of several players: for each sub-question that ANY
    /// player constrains, the acceptable labels are the intersection across the
    /// players who constrain it (a player silent on a key imposes no limit). Used
    /// to reason about what configurations could possibly satisfy everyone.
    /// </summary>
    public static AcceptanceRegion Intersect(IEnumerable<AcceptanceRegion> regions)
    {
        var list = regions.ToList();
        var allKeys = list.SelectMany(r => r.ConstrainedKeys).Distinct(StringComparer.OrdinalIgnoreCase);

        var combined = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in allKeys)
        {
            // Only regions that constrain this key restrict it; intersect their sets.
            var constrainers = list.Where(r => r.ConstrainedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();
            IEnumerable<string>? acc = null;
            foreach (var r in constrainers)
            {
                var labels = r.AcceptableLabels(key);
                acc = acc is null ? labels : acc.Where(l => labels.Contains(l));
            }
            combined[key] = (acc ?? Enumerable.Empty<string>()).ToArray();
        }
        return new AcceptanceRegion(combined);
    }

    /// <summary>
    /// Sub-questions on which the players are irreconcilable: the intersection of
    /// acceptable labels is empty, so NO version resolving that sub-question can
    /// satisfy everyone (only a version silent on it can). Informational signal
    /// for distance/fork reasoning.
    /// </summary>
    public static IReadOnlyList<string> IrreconcilableKeys(IEnumerable<AcceptanceRegion> regions)
    {
        var list = regions.ToList();
        var allKeys = list.SelectMany(r => r.ConstrainedKeys).Distinct(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var key in allKeys)
        {
            var constrainers = list.Where(r => r.ConstrainedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();
            if (constrainers.Count < 2) continue;
            IEnumerable<string>? acc = null;
            foreach (var r in constrainers)
            {
                var labels = r.AcceptableLabels(key);
                acc = acc is null ? labels : acc.Where(l => labels.Contains(l));
            }
            if (!(acc ?? Enumerable.Empty<string>()).Any()) result.Add(key);
        }
        return result;
    }
}
