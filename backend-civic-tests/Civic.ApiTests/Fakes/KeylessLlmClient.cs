using Arena.Shared.Llm;

namespace Civic.ApiTests.Fakes;

/// <summary>
/// Simulates the dev/no-key environment: every call throws <see cref="LlmException"/>,
/// exactly as <c>ClaudeLlmClient</c> does when <c>Anthropic:ApiKey</c> is unset. Used to
/// exercise the graceful-fallback paths of the coalition judges.
/// </summary>
public sealed class KeylessLlmClient : ILlmClient
{
    public Task<T> GenerateStructuredAsync<T>(
        string systemPrompt, string userPrompt, LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null, CancellationToken ct = default)
        => throw new LlmException("Anthropic:ApiKey not configured.");
}
