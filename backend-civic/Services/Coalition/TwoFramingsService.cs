using Arena.Shared.Llm;
using Microsoft.Extensions.Logging;

namespace Civic.API.Services.Coalition;

/// <summary>A story shown in its two framings (doc 02): the cultural (tribal) frame and the governable one.</summary>
public sealed record TwoFramingsResult(string CulturalFrame, string GovernanceFrame, bool FromLlm);

public interface ITwoFramingsService
{
    Task<TwoFramingsResult> ForAsync(
        string title, string neutralText, IReadOnlyList<string> relevantAxes,
        string? disagreement, IReadOnlyList<string> valuesInConflict, CancellationToken ct = default);
}

public class TwoFramingsDto
{
    public string CulturalFrame { get; set; } = "";
    public string GovernanceFrame { get; set; } = "";
}

/// <summary>
/// Produces a story's cultural vs governance framings (doc 02 — "surfacing that gap is
/// the lesson"). LLM in prod (gated to premium via the optional access policy); heuristic
/// fallback otherwise so it always returns something usable.
/// </summary>
public class TwoFramingsService : ITwoFramingsService
{
    private readonly ILlmClient _llm;
    private readonly ILlmAccessPolicy? _policy;
    private readonly ILogger<TwoFramingsService> _log;

    public TwoFramingsService(ILlmClient llm, ILogger<TwoFramingsService> log, ILlmAccessPolicy? policy = null)
    {
        _llm = llm;
        _log = log;
        _policy = policy;
    }

    public async Task<TwoFramingsResult> ForAsync(
        string title, string neutralText, IReadOnlyList<string> relevantAxes,
        string? disagreement, IReadOnlyList<string> valuesInConflict, CancellationToken ct = default)
    {
        try
        {
            if (_policy is not null && !_policy.CanUseLlm())
                throw new LlmException("LLM access requires a premium account.");

            var (sys, user) = Prompt(title, neutralText, relevantAxes, disagreement, valuesInConflict);
            var dto = await _llm.GenerateStructuredAsync<TwoFramingsDto>(sys, user, LlmModelTier.Haiku, maxTokens: 300, ct: ct);
            if (string.IsNullOrWhiteSpace(dto.CulturalFrame) || string.IsNullOrWhiteSpace(dto.GovernanceFrame))
                return Fallback(neutralText, disagreement, valuesInConflict);
            return new TwoFramingsResult(dto.CulturalFrame.Trim(), dto.GovernanceFrame.Trim(), FromLlm: true);
        }
        catch (LlmException ex)
        {
            _log.LogDebug(ex, "Two-framings falling back to heuristic.");
            return Fallback(neutralText, disagreement, valuesInConflict);
        }
    }

    private static TwoFramingsResult Fallback(string neutralText, string? disagreement, IReadOnlyList<string> values)
    {
        var cultural = !string.IsNullOrWhiteSpace(disagreement)
            ? disagreement!
            : values.Count > 0
                ? $"Culturally, this becomes a clash over {string.Join(" vs. ", values.Take(2))} — identity-shaped and hard to bridge."
                : "Culturally, this reads as a tribal us-vs-them story.";
        var governance = string.IsNullOrWhiteSpace(neutralText)
            ? "The governable question: what should this specific institution actually do?"
            : neutralText;
        return new TwoFramingsResult(cultural, governance, FromLlm: false);
    }

    private static (string System, string User) Prompt(
        string title, string neutralText, IReadOnlyList<string> axes, string? disagreement, IReadOnlyList<string> values) =>
    (
        """
        Show a news story in its TWO framings so a reader can see which produces agreement:
        - culturalFrame: the tribal/identity framing (often irreconcilable).
        - governanceFrame: the governable question underneath (often bridgeable) — institutions,
          mechanisms, tradeoffs.
        Respond with ONLY JSON. No prose. Shape: {"culturalFrame": "<1-2 sentences>", "governanceFrame": "<1-2 sentences>"}
        """,
        $$"""
        Title: {{title}}
        Governance proposition: {{neutralText}}
        Values axes: {{string.Join(", ", axes)}}
        What people disagree about: {{disagreement ?? "(unknown)"}}
        Values in conflict: {{string.Join(", ", values)}}

        Produce the two framings JSON.
        """
    );
}
