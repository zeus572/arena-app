namespace Civic.API.Services.Coalition.Geometry;

/// <summary>
/// Result of <see cref="DistanceCalculator.DistanceToCoalition"/>: the best
/// available version and how far it is from sitting inside every required
/// acceptance set.
/// </summary>
public sealed record DistanceResult(
    int RequiredCount,
    int Uncovered,
    double Normalized,
    VersionPoint? BestVersion,
    IReadOnlyList<string> MissingUserIds);

/// <summary>
/// Phase 1.2 — distance-to-coalition. Pure computation (no LLM).
///
/// distanceToCoalition = how far the BEST available spanning version is from
/// sitting in ENOUGH acceptance sets. Modeled as: over the candidate versions,
/// the minimum number of required players whose region does not contain the
/// version (the gap). Smaller = closer; 0 = a version everyone in the required
/// set would co-sign. Amendments add candidate versions, so a carve-out that
/// pulls a previously-excluded corner in strictly reduces the minimum gap (doc 06:
/// "distance closes when an amendment reshapes the configuration so it lands inside
/// more acceptance sets").
///
/// "Enough acceptance sets" + "spanning" is supplied here as the REQUIRED player
/// set (the spectrum-spanning members that must be covered); whether that set is
/// values-broad is measured separately by <see cref="BreadthCalculator"/> and
/// combined by the (later) game loop. Recorded assumption.
/// </summary>
public static class DistanceCalculator
{
    public static DistanceResult DistanceToCoalition(
        IEnumerable<VersionPoint> versions,
        IReadOnlyList<PlayerGeometry> requiredPlayers)
    {
        var required = requiredPlayers.ToList();
        if (required.Count == 0)
            return new DistanceResult(0, 0, 0.0, null, Array.Empty<string>());

        var versionList = versions.ToList();
        if (versionList.Count == 0)
            return new DistanceResult(required.Count, required.Count, 1.0, null,
                required.Select(p => p.UserId).ToList());

        DistanceResult? best = null;
        foreach (var v in versionList)
        {
            var missing = required.Where(p => !p.Region.Contains(v)).Select(p => p.UserId).ToList();
            var candidate = new DistanceResult(
                required.Count, missing.Count, (double)missing.Count / required.Count, v, missing);

            if (best is null || IsBetter(candidate, best))
                best = candidate;
        }
        return best!;
    }

    // Best = fewest uncovered; tie-break toward the more specific version (more
    // teeth) so the reported best spanning version isn't a toothless silent one.
    private static bool IsBetter(DistanceResult candidate, DistanceResult incumbent)
    {
        if (candidate.Uncovered != incumbent.Uncovered)
            return candidate.Uncovered < incumbent.Uncovered;
        var candSpec = candidate.BestVersion?.Specificity ?? -1;
        var incSpec = incumbent.BestVersion?.Specificity ?? -1;
        return candSpec > incSpec;
    }
}
