using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Pure, rule-based candidate↔briefing matching. Deliberately NOT LLM-based so
/// fan-out cost is predictable. Scores a candidate by how well the briefing's
/// issue tags overlap the candidate's platform planks and source library.
/// </summary>
public static class CandidateSelection
{
    /// <summary>
    /// 0..1 overlap score between a briefing's tags and a candidate's issue
    /// tags (drawn from planks + sources). Jaccard-style: matched / briefing tags.
    /// </summary>
    public static double IssueMatchScore(
        IEnumerable<string> candidateIssueTags, IEnumerable<string> briefingTags)
    {
        var candidate = Normalize(candidateIssueTags);
        var briefing = Normalize(briefingTags);
        if (briefing.Count == 0 || candidate.Count == 0) return 0;

        var matched = briefing.Count(b =>
            candidate.Contains(b) || candidate.Any(c => c.Contains(b) || b.Contains(c)));
        return (double)matched / briefing.Count;
    }

    public static IReadOnlyCollection<string> CandidateTags(VirtualCandidate c) =>
        c.PlatformPlanks.SelectMany(p => p.IssueTags)
            .Concat(c.Sources.SelectMany(s => s.IssueTags))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static HashSet<string> Normalize(IEnumerable<string> tags) =>
        tags.Select(t => (t ?? "").Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-'))
            .Where(t => t.Length > 0)
            .ToHashSet();
}
