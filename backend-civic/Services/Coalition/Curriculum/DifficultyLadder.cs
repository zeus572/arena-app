namespace Civic.API.Services.Coalition.Curriculum;

/// <summary>One past provision result for a group: the (normalized) gap width it faced and whether it closed a coalition.</summary>
public sealed record CircleOutcome(double GapWidth, bool Closed);

/// <summary>A group's bridging track record.</summary>
public sealed record CircleHistory(IReadOnlyList<CircleOutcome> Outcomes)
{
    public int Attempted => Outcomes.Count;
    public int Closed => Outcomes.Count(o => o.Closed);
    public double SuccessRate => Attempted == 0 ? 0 : (double)Closed / Attempted;
    /// <summary>The widest gap this group has successfully bridged (0 if none).</summary>
    public double MaxClosedGap => Outcomes.Where(o => o.Closed).Select(o => o.GapWidth).DefaultIfEmpty(0).Max();
}

/// <summary>Per-group skill estimate from bridging track record.</summary>
public static class GroupSkill
{
    /// <summary>
    /// Skill in [0,1]: a blend of how reliably the group closes coalitions and how wide a gap
    /// it has managed to bridge. A brand-new circle (no history) is skill 0.
    /// </summary>
    public static double Estimate(CircleHistory history)
    {
        if (history.Attempted == 0) return 0.0;
        return Math.Clamp(0.5 * history.SuccessRate + 0.5 * history.MaxClosedGap, 0.0, 1.0);
    }
}

/// <summary>
/// Phase 3.2 — difficulty laddering. Serve narrow-gap provisions to new/low-skill
/// groups and widen as the group's bridging track record grows. Pure.
/// </summary>
public static class DifficultyLadder
{
    /// <summary>The gap width to aim for given a group's skill (monotonic; narrow for new groups).</summary>
    public static double TargetGap(double skill) => Math.Clamp(skill, 0.0, 1.0);

    /// <summary>Pick the candidate whose gap width best matches the group's skill-target.</summary>
    public static T Serve<T>(double skill, IReadOnlyList<T> candidates, Func<T, double> gapOf)
    {
        if (candidates.Count == 0) throw new ArgumentException("No candidate provisions to serve.", nameof(candidates));
        var target = TargetGap(skill);
        return candidates
            .OrderBy(c => Math.Abs(gapOf(c) - target))
            .ThenBy(gapOf) // tie-break toward the narrower of two equidistant options
            .First();
    }

    /// <summary>Convenience: the served gap width for a skill from a pool of gap widths.</summary>
    public static double ServedGap(double skill, IReadOnlyList<double> gaps) => Serve(skill, gaps, g => g);
}
