using Arena.Shared.Llm;

namespace Civic.ApiTests.Fakes;

/// <summary>
/// Simulates a LIVE Anthropic failure (e.g. out of credits / 429 / 5xx): the key is
/// configured and a call WAS attempted, but it failed. Distinct from <see cref="KeylessLlmClient"/>
/// (which models the by-design dev/no-key state). Used to prove that synthesis/birth operations
/// BAIL on <see cref="LlmFailureKind.CallFailed"/> instead of persisting heuristic "dead" data.
/// </summary>
public sealed class FailingLlmClient : ILlmClient
{
    public Task<T> GenerateStructuredAsync<T>(
        string systemPrompt, string userPrompt, LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null, CancellationToken ct = default)
        => throw new LlmException(
            "Anthropic API returned 400 Bad Request.",
            kind: LlmFailureKind.CallFailed);
}
