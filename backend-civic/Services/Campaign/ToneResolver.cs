using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Resolves the (tone, intensity) for a post deterministically BEFORE the LLM
/// call — tone/intensity is a system decision, never driven by a post's
/// reception. Per the PRD: look up the candidate's per-issue override, fall
/// back to their default, then apply a small deterministic jitter.
/// </summary>
public static class ToneResolver
{
    public static (CampaignTone Tone, int Intensity) Resolve(
        VirtualCandidate candidate, IEnumerable<string> issueTags, int seed)
    {
        var tags = issueTags.Select(Normalize).Where(t => t.Length > 0).ToHashSet();

        var match = candidate.IssueTones.FirstOrDefault(t =>
        {
            var issue = Normalize(t.Issue);
            return tags.Contains(issue) || tags.Any(tag => tag.Contains(issue) || issue.Contains(tag));
        });

        var tone = match?.Tone ?? candidate.DefaultTone;
        var baseIntensity = match?.Intensity ?? candidate.DefaultIntensity;

        // Deterministic ±1 jitter so a candidate isn't locked to one register.
        var jitter = Math.Abs(seed) % 3 - 1;
        var intensity = Math.Clamp(baseIntensity + jitter, 1, 5);

        return (tone, intensity);
    }

    private static string Normalize(string s) =>
        (s ?? "").Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
}
