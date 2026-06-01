using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Deterministically derives the "salient issues" in play for a given campaign week. Salience is
/// seeded from the issues the race's candidates actually campaign on, rotated per week so the
/// environment shifts over a campaign. Pure and reproducible (no live data dependency); this is
/// where a real news/polling salience feed would later plug in.
/// </summary>
public static class CivicSalience
{
    /// <summary>
    /// The salient issues for <paramref name="week"/>, drawn from the race's combined issue pool.
    /// A stable per-(seed, week) rotation picks <paramref name="count"/> issues so weeks differ but
    /// replays are identical.
    /// </summary>
    public static List<string> ForWeek(IEnumerable<VirtualCandidate> raceCandidates, int week, int seed, int count = 3)
    {
        var pool = raceCandidates
            .SelectMany(CivicCampaignFit.CandidateIssues)
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase) // stable base ordering
            .ToList();

        if (pool.Count == 0) return new List<string>();
        if (pool.Count <= count) return pool;

        // Deterministic rotation: offset by a hash of (seed, week) so each week surfaces a
        // different, reproducible slice of the pool.
        var offset = Math.Abs(HashCode.Combine(seed, week)) % pool.Count;
        var result = new List<string>(count);
        for (var i = 0; i < count; i++)
            result.Add(pool[(offset + i) % pool.Count]);
        return result;
    }

    /// <summary>
    /// Salience weight (0..1) of a single issue this week: 1.0 if it's a top salient issue, with a
    /// modest floor for non-salient issues so off-theme actions still do something small.
    /// </summary>
    public static double Weight(IReadOnlyList<string> salientIssues, string issue)
    {
        if (string.IsNullOrWhiteSpace(issue) || salientIssues.Count == 0) return 0.3;
        for (var i = 0; i < salientIssues.Count; i++)
        {
            if (string.Equals(salientIssues[i], issue, StringComparison.OrdinalIgnoreCase)
                || salientIssues[i].Contains(issue, StringComparison.OrdinalIgnoreCase)
                || issue.Contains(salientIssues[i], StringComparison.OrdinalIgnoreCase))
            {
                // Top issue = 1.0, decaying slightly down the list.
                return CivicSupportModel.Clamp(1.0 - i * 0.15, 0.5, 1.0);
            }
        }
        return 0.3; // not salient this week
    }
}
