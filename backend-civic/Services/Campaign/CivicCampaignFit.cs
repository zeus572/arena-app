using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Pure helpers for computing how well a candidate fits a given issue, used to drive support
/// deltas. Fit is derived from platform-plank/source issue-tag overlap (a candidate is strong on
/// issues they actually campaign on). DB-free and deterministic.
/// </summary>
public static class CivicCampaignFit
{
    /// <summary>
    /// Fit of a candidate to a single issue tag, in [-1, 1]. +1 when the issue is central to the
    /// candidate's platform; mildly negative when it appears nowhere in their planks or sources.
    /// </summary>
    public static double IssueFit(VirtualCandidate candidate, string issue)
    {
        if (string.IsNullOrWhiteSpace(issue)) return 0;

        var plankHits = candidate.PlatformPlanks
            .Count(p => p.IssueTags.Any(t => TagsMatch(t, issue)));
        var sourceHits = candidate.Sources
            .Count(s => s.IssueTags.Any(t => TagsMatch(t, issue)));

        if (plankHits == 0 && sourceHits == 0)
            return -0.4; // off-brand: the candidate doesn't really own this issue

        // Planks weigh more than sources; saturate quickly.
        var raw = plankHits * 0.6 + sourceHits * 0.25;
        return CivicSupportModel.Clamp(raw, 0, 1);
    }

    /// <summary>The set of issue tags a candidate is associated with (planks + sources).</summary>
    public static IReadOnlyList<string> CandidateIssues(VirtualCandidate candidate)
    {
        return candidate.PlatformPlanks.SelectMany(p => p.IssueTags)
            .Concat(candidate.Sources.SelectMany(s => s.IssueTags))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Average fit of a candidate across a set of issues (e.g. the week's salient ones).</summary>
    public static double AverageFit(VirtualCandidate candidate, IEnumerable<string> issues)
    {
        var list = issues.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (list.Count == 0) return 0;
        return list.Average(i => IssueFit(candidate, i));
    }

    private static bool TagsMatch(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
           || a.Contains(b, StringComparison.OrdinalIgnoreCase)
           || b.Contains(a, StringComparison.OrdinalIgnoreCase);
}
