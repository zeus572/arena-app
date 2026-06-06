namespace Civic.API.Services.Coalition.Curriculum;

// =====================================================================
// Phase 3.4 — campaign milestones & promotion/relegation. Pure computation
// (no LLM); the most empirical layer, calibrated against observed play.
// =====================================================================

/// <summary>A passed plank deposited into a player's legislative record.</summary>
public sealed record PassedPlank(
    double GapWidthAtBirth, // how hard it was to bridge at birth (3.1), normalized
    int Breadth,            // spectrum buckets the coalition spanned
    int Specificity,        // teeth (sub-questions resolved)
    int MovedSigners,       // how many bargained in
    bool IsGovernance);     // governance plank vs culture-layer

/// <summary>A campaign's accrued record over the election calendar.</summary>
public sealed record CampaignSummary(
    int PlanksPassed,
    int TotalBreadth,
    double AvgBreadth,
    int TotalMovedSigners,
    double GovernanceRatio,  // governance planks / all planks (culture-vs-governance health)
    double WeightedScore);   // payout-coupled: breadth x (1 + gap), so bridging hard provisions is worth more

/// <summary>
/// Accrues passed planks into a campaign record + the coalition-breadth meter +
/// the governance-vs-culture ratio. Payout coupling (doc 06 "still open"): a plank's
/// contribution scales with the gap it closed, so genuinely polarized provisions are
/// worth dramatically more than easy ones.
/// </summary>
public static class CampaignMilestones
{
    public static CampaignSummary Accrue(IReadOnlyList<PassedPlank> planks)
    {
        if (planks.Count == 0)
            return new CampaignSummary(0, 0, 0, 0, 0, 0);

        var totalBreadth = planks.Sum(p => p.Breadth);
        var totalMoved = planks.Sum(p => p.MovedSigners);
        var governance = planks.Count(p => p.IsGovernance);
        var weighted = planks.Sum(p => p.Breadth * (1.0 + p.GapWidthAtBirth));

        return new CampaignSummary(
            PlanksPassed: planks.Count,
            TotalBreadth: totalBreadth,
            AvgBreadth: (double)totalBreadth / planks.Count,
            TotalMovedSigners: totalMoved,
            GovernanceRatio: (double)governance / planks.Count,
            WeightedScore: weighted);
    }
}

public enum LeagueMovement { Relegate, Stay, Promote }

/// <summary>
/// Promotion/relegation keeps players near their ability edge: an over-skilled
/// group is promoted to a wider-gap league, a struggling one relegated. Pure.
/// (Skill comes from <see cref="GroupSkill"/>; league "gap tier" is its served gap
/// width in [0,1].)
/// </summary>
public static class PromotionService
{
    public static LeagueMovement Decide(double skill, double leagueGapTier, double margin = 0.15)
    {
        if (skill > leagueGapTier + margin) return LeagueMovement.Promote;
        if (skill < leagueGapTier - margin) return LeagueMovement.Relegate;
        return LeagueMovement.Stay;
    }

    /// <summary>The tier a player moves to on a promotion/relegation, from a ladder of tiers (ascending).</summary>
    public static double NextTier(LeagueMovement movement, double currentTier, IReadOnlyList<double> tiers)
    {
        if (tiers.Count == 0) return currentTier;
        var ordered = tiers.OrderBy(t => t).ToList();
        var idx = ordered.FindIndex(t => Math.Abs(t - currentTier) < 1e-9);
        if (idx < 0) idx = ClosestIndex(ordered, currentTier);

        var next = movement switch
        {
            LeagueMovement.Promote => Math.Min(idx + 1, ordered.Count - 1),
            LeagueMovement.Relegate => Math.Max(idx - 1, 0),
            _ => idx,
        };
        return ordered[next];
    }

    private static int ClosestIndex(IReadOnlyList<double> tiers, double value)
    {
        var best = 0;
        for (var i = 1; i < tiers.Count; i++)
            if (Math.Abs(tiers[i] - value) < Math.Abs(tiers[best] - value)) best = i;
        return best;
    }
}

/// <summary>
/// Soft campaign-participation cadence — rewards consistency WITHOUT all-or-nothing
/// breakage (no hard streak that resets to zero on one missed day). Score is the
/// recency-weighted fraction of active days over a window, so a single miss only
/// nudges it down. Pure.
/// </summary>
public static class CampaignCadence
{
    /// <summary>Recency-weighted coverage in [0,1] (most-recent day last in the list).</summary>
    public static double Score(IReadOnlyList<bool> dailyActive)
    {
        if (dailyActive.Count == 0) return 0;
        double num = 0, den = 0;
        for (var i = 0; i < dailyActive.Count; i++)
        {
            var weight = i + 1; // more recent days weigh more
            den += weight;
            if (dailyActive[i]) num += weight;
        }
        return num / den;
    }

    /// <summary>A hard streak (consecutive active days from the end) — kept only to contrast the soft cadence.</summary>
    public static int HardStreak(IReadOnlyList<bool> dailyActive)
    {
        var streak = 0;
        for (var i = dailyActive.Count - 1; i >= 0 && dailyActive[i]; i--) streak++;
        return streak;
    }
}
