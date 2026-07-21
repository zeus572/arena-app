using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arena.Shared.Llm;

/// <summary>
/// Runs a <b>primary</b> LLM provider (Claude) and, when a request fails, retries it once
/// on a <b>backup</b> provider (GPT). This is the runtime backup the app is wired through:
/// if Anthropic is rate-limited, out of credits, times out, or is turned off on this box,
/// the same structured-JSON request is re-issued against OpenAI so callers keep working.
///
/// <para>The fallback fires on every failure mode a caller would otherwise see from the
/// primary: an <see cref="LlmException"/> of any <see cref="LlmFailureKind"/> (including
/// <see cref="LlmFailureKind.Unavailable"/> — a disabled/keyless primary routes to the
/// backup), a transport <see cref="HttpRequestException"/>, and a request-timeout
/// <see cref="OperationCanceledException"/>. Caller-initiated cancellation is never treated
/// as a failure — it propagates untouched.</para>
///
/// <para>If BOTH providers fail, the surfaced exception carries the more severe failure kind
/// (CallFailed &gt; BadResponse &gt; Unavailable) so downstream behaviour is unchanged: a
/// genuine outage still tells batch callers to bail, while "both unavailable" still lets
/// on-demand callers fall back to their heuristics. When the backup is unconfigured this
/// degrades exactly to primary-only behaviour.</para>
/// </summary>
public class FallbackLlmClient : ILlmClient
{
    private readonly ILlmClient _primary;
    private readonly ILlmClient _backup;
    private readonly ILogger _logger;

    public FallbackLlmClient(
        ILlmClient primary,
        ILlmClient backup,
        ILogger<FallbackLlmClient>? logger = null)
    {
        _primary = primary;
        _backup = backup;
        _logger = logger ?? NullLogger<FallbackLlmClient>.Instance;
    }

    public async Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        try
        {
            return await _primary.GenerateStructuredAsync<T>(systemPrompt, userPrompt, tier, maxTokens, ct);
        }
        catch (Exception primaryEx) when (ShouldFallBack(primaryEx, ct))
        {
            _logger.LogWarning(
                primaryEx, "Primary LLM failed ({Kind}); falling back to backup provider.", KindOf(primaryEx));
            try
            {
                return await _backup.GenerateStructuredAsync<T>(systemPrompt, userPrompt, tier, maxTokens, ct);
            }
            catch (Exception backupEx) when (backupEx is LlmException or HttpRequestException
                                             || (backupEx is OperationCanceledException && !ct.IsCancellationRequested))
            {
                throw Merge(primaryEx, backupEx);
            }
        }
    }

    /// <summary>
    /// A failure is worth retrying on the backup unless it is caller-initiated cancellation
    /// (which must propagate). LLM errors, transport errors, and request timeouts all qualify.
    /// </summary>
    private static bool ShouldFallBack(Exception ex, CancellationToken ct) => ex switch
    {
        OperationCanceledException => !ct.IsCancellationRequested,
        LlmException => true,
        HttpRequestException => true,
        _ => false,
    };

    private static LlmException Merge(Exception primaryEx, Exception backupEx)
    {
        var kind = Severity(KindOf(backupEx)) >= Severity(KindOf(primaryEx))
            ? KindOf(backupEx)
            : KindOf(primaryEx);

        // Prefer a raw body from whichever leg produced one (the backup last).
        var raw = (backupEx as LlmException)?.RawResponse ?? (primaryEx as LlmException)?.RawResponse;

        return new LlmException(
            $"Both LLM providers failed. Primary: {primaryEx.Message} | Backup: {backupEx.Message}",
            rawResponse: raw,
            inner: backupEx,
            kind: kind);
    }

    // A transport failure or timeout is an outage-class failure, so map it to CallFailed.
    private static LlmFailureKind KindOf(Exception ex) =>
        ex is LlmException le ? le.Kind : LlmFailureKind.CallFailed;

    private static int Severity(LlmFailureKind kind) => kind switch
    {
        LlmFailureKind.CallFailed => 2,
        LlmFailureKind.BadResponse => 1,
        _ => 0,
    };
}
