using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Arena.Shared.Llm;

/// <summary>
/// Shared skeleton for the structured-JSON LLM clients (Claude, GPT). Owns the
/// availability gate, the single-JSON-retry loop, and the failure-kind tagging so
/// every provider behaves identically to callers; subclasses supply only the
/// provider-specific bits: how to resolve a model from a tier and how to make one
/// live call. Keeping the retry/salvage policy in one place is what lets
/// <see cref="FallbackLlmClient"/> treat any provider as a drop-in for another.
/// </summary>
public abstract class StructuredJsonLlmClient : ILlmClient
{
    private readonly ILogger _logger;

    protected StructuredJsonLlmClient(ILogger logger) => _logger = logger;

    /// <summary>Human label for logs and error messages, e.g. "Claude" / "GPT".</summary>
    protected abstract string ProviderName { get; }

    /// <summary>
    /// Return null when the provider is ready to call; otherwise a reason it is
    /// unavailable BY DESIGN (kill-switch off, no API key). A non-null value is thrown as
    /// <see cref="LlmException"/> with <see cref="LlmFailureKind.Unavailable"/>, so on-demand
    /// callers fall back to heuristics rather than treating it as an outage.
    /// </summary>
    protected abstract string? UnavailableReason();

    /// <summary>Map the requested tier to this provider's concrete model id.</summary>
    protected abstract string ResolveModel(LlmModelTier tier);

    /// <summary>
    /// Perform one live call and return the assistant's raw text. Implementations MUST throw
    /// <see cref="LlmException"/> with <see cref="LlmFailureKind.CallFailed"/> on any
    /// transport/HTTP failure or malformed envelope, so batch callers bail instead of
    /// persisting dead data.
    /// </summary>
    protected abstract Task<string> CallAsync(
        string model, string systemPrompt, string userPrompt, int? maxTokens, CancellationToken ct);

    public async Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        var reason = UnavailableReason();
        if (reason is not null)
        {
            throw new LlmException(reason);
        }

        var model = ResolveModel(tier);
        var raw = await CallAsync(model, systemPrompt, userPrompt, maxTokens, ct);
        try
        {
            return LlmJson.Parse<T>(raw);
        }
        catch (JsonException)
        {
            _logger.LogWarning("{Provider} returned non-JSON; retrying with a stricter reminder.", ProviderName);
            var rawRetry = await CallAsync(
                model,
                systemPrompt + "\n\nIMPORTANT: respond with ONLY a single JSON value. No prose, no markdown fences.",
                userPrompt,
                maxTokens,
                ct);
            try
            {
                return LlmJson.Parse<T>(rawRetry);
            }
            catch (JsonException jex)
            {
                // The call itself succeeded (HTTP 200) — the model just wouldn't emit the
                // requested JSON, even after a stricter reminder. That's a per-request content
                // failure (a prose preamble/refusal, or truncation), NOT an API outage, so it
                // must be tagged BadResponse: batch callers fail just this item and keep going
                // instead of halting and re-queuing a poison pill at the head of the batch.
                _logger.LogWarning(
                    "{Provider} returned non-JSON after retry; first {N} chars: {Snippet}",
                    ProviderName, 120, LlmJson.Snippet(rawRetry, 120));
                throw new LlmException(
                    $"{ProviderName} returned non-JSON after retry.",
                    rawResponse: rawRetry,
                    inner: jex,
                    kind: LlmFailureKind.BadResponse);
            }
        }
    }
}
