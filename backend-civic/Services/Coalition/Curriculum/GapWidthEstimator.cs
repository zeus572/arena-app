using Civic.API.Models;
using Civic.API.Services.Coalition.Agents;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Curriculum;

// =====================================================================
// LAYER 3 — ladder / leagues / curriculum (the most empirical layer). Pure
// computation; calibrated against observed self-play. No LLM.
// =====================================================================

/// <summary>
/// Phase 3.1 — estimate a provision's expected GAP WIDTH at birth: how disjoint the
/// league's acceptance sets are on this provision, i.e. how much bridging work it
/// will take to close. The curriculum (3.2) sorts provisions by this. Pure.
///
/// Birth-time proxy: the intensity-weighted total "distance from the neutral base"
/// across the league — every position a member would NOT co-sign in the base
/// version adds its intensity weight. More demanding corners, and harder-held
/// (higher-intensity / NonNegotiable) demands, mean a wider gap. Calibrated below
/// against observed closure difficulty (the test asserts the rank correlation).
/// </summary>
public static class GapWidthEstimator
{
    private static double Weight(AnswerIntensity i) => i switch
    {
        AnswerIntensity.Low => 1,
        AnswerIntensity.Medium => 2,
        AnswerIntensity.High => 3,
        AnswerIntensity.NonNegotiable => 4,
        _ => 1,
    };

    /// <summary>Raw gap score (unbounded; monotone in disagreement). Use for ranking provisions.</summary>
    public static double EstimateAtBirth(IReadOnlyList<CoalitionAgent> league, VersionPoint baseVersion)
    {
        double gap = 0;

        // (1) Base-rejection mass: how far the league sits from co-signing the neutral base.
        foreach (var member in league)
        {
            foreach (var (key, label) in baseVersion.Positions)
            {
                if (member.Region.ConstrainedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                    && !member.Region.AcceptableLabels(key).Contains(label))
                {
                    gap += Weight(member.IntensityFor(key)); // an unmet, intensity-weighted demand
                }
            }
        }

        // (2) Irreconcilability mass: sub-questions where members' acceptable sets don't
        // intersect at all (no resolving version can satisfy everyone — only a carve-out or a
        // fork can). This captures disjoint poles even when the base happens to sit at one
        // pole (so one side "accepts" it and the base-rejection term alone would understate
        // the gap).
        var regions = league.Select(a => a.Region).ToList();
        foreach (var key in OverlapCalculator.IrreconcilableKeys(regions))
        {
            var w = league
                .Where(a => a.Region.ConstrainedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                .Select(a => Weight(a.IntensityFor(key)))
                .DefaultIfEmpty(1)
                .Max();
            gap += w;
        }

        return gap;
    }

    /// <summary>Gap normalized to [0,1] for display / difficulty bucketing.</summary>
    public static double NormalizedGap(IReadOnlyList<CoalitionAgent> league, VersionPoint baseVersion)
    {
        var raw = EstimateAtBirth(league, baseVersion);
        var max = league.Count * Weight(AnswerIntensity.NonNegotiable) * Math.Max(1, baseVersion.Positions.Count);
        return max <= 0 ? 0 : Math.Min(1.0, raw / max);
    }
}
