using System.Diagnostics;
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
            // No live call is made, so there is no model or latency to report — but still emit
            // the data point so "provider X is turned off / keyless" shows up in usage dashboards
            // rather than looking like silence.
            LogCall(model: null, tier, outcome: "unavailable", retried: false, elapsedMs: 0, ex: null);
            throw new LlmException(reason);
        }

        var model = ResolveModel(tier);
        var startedAt = Stopwatch.GetTimestamp();
        var retried = false;
        try
        {
            var raw = await CallAsync(model, systemPrompt, userPrompt, maxTokens, ct);
            T parsed;
            try
            {
                parsed = LlmJson.Parse<T>(raw);
            }
            catch (JsonException)
            {
                retried = true;
                _logger.LogWarning("{Provider} returned non-JSON; retrying with a stricter reminder.", ProviderName);
                var rawRetry = await CallAsync(
                    model,
                    systemPrompt + "\n\nIMPORTANT: respond with ONLY a single JSON value. No prose, no markdown fences.",
                    userPrompt,
                    maxTokens,
                    ct);
                try
                {
                    parsed = LlmJson.Parse<T>(rawRetry);
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

            LogCall(model, tier, outcome: "success", retried, ElapsedMs(startedAt), ex: null);
            return parsed;
        }
        catch (Exception ex)
        {
            // One data point per live call, on every exit path, so usage/failure dashboards see
            // which provider+model was hit and how it fared. Caller cancellation is normal
            // control flow, not a failure of this provider — record it as such (info, not warn).
            LogCall(model, tier, OutcomeOf(ex, ct), retried, ElapsedMs(startedAt), IsCallerCancel(ex, ct) ? null : ex);
            throw;
        }
    }

    /// <summary>
    /// Emit a single structured usage/health data point for one provider call. The named
    /// placeholders surface as App Insights <c>customDimensions</c> (LlmProvider, LlmModel,
    /// LlmOutcome, …), so you can chart which models/APIs are being used and their success vs
    /// failure split, and alert on outcome=call_failed spikes. Failures log at Warning (with the
    /// exception attached); everything else at Information.
    /// </summary>
    private void LogCall(string? model, LlmModelTier tier, string outcome, bool retried, long elapsedMs, Exception? ex)
    {
        const string template =
            "LLM call provider={LlmProvider} model={LlmModel} tier={LlmTier} outcome={LlmOutcome} retried={LlmRetried} latencyMs={LlmLatencyMs}";
        var modelName = model ?? "(none)";
        if (ex is null)
        {
            _logger.LogInformation(template, ProviderName, modelName, tier, outcome, retried, elapsedMs);
        }
        else
        {
            _logger.LogWarning(ex, template, ProviderName, modelName, tier, outcome, retried, elapsedMs);
        }
    }

    private static long ElapsedMs(long startedAt) => (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private static bool IsCallerCancel(Exception ex, CancellationToken ct) =>
        ex is OperationCanceledException && ct.IsCancellationRequested;

    // Map an exception to a stable outcome token for the telemetry dimension. Kept coarse so the
    // dashboard groups cleanly: the LlmFailureKind values plus the two non-LlmException shapes a
    // live call can throw (a request timeout vs. caller cancellation, and a raw transport error).
    private static string OutcomeOf(Exception ex, CancellationToken ct) => ex switch
    {
        LlmException le => le.Kind switch
        {
            LlmFailureKind.CallFailed => "call_failed",
            LlmFailureKind.BadResponse => "bad_response",
            LlmFailureKind.Unavailable => "unavailable",
            _ => "error",
        },
        OperationCanceledException => ct.IsCancellationRequested ? "canceled" : "timeout",
        HttpRequestException => "call_failed",
        _ => "error",
    };
}
