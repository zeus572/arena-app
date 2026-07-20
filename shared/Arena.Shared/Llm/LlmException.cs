namespace Arena.Shared.Llm;

/// <summary>
/// Why an <see cref="LlmException"/> was raised — the distinction that lets callers
/// decide between falling back to a heuristic and bailing out entirely.
/// </summary>
public enum LlmFailureKind
{
    /// <summary>
    /// The LLM is unavailable BY DESIGN, not because a call failed: the kill-switch is
    /// off (<c>Anthropic:Enabled=false</c>), no API key is configured, or a policy gate
    /// denied this caller (e.g. the coalition premium gate). This is the normal dev/local
    /// and free-tier state, so on-demand callers SHOULD fall back to their heuristics.
    /// </summary>
    Unavailable,

    /// <summary>
    /// A live API call WAS attempted and failed at the transport/HTTP layer — out of
    /// credits, rate-limited (429), overloaded (529), a 5xx, or a malformed envelope. This
    /// affects EVERY request equally (the API itself is unhealthy), so batch callers should
    /// halt and retry the whole batch later rather than hammer a dead API, and on-demand
    /// callers should surface a retryable error instead of persisting "dead" heuristic data.
    /// </summary>
    CallFailed,

    /// <summary>
    /// The call SUCCEEDED (HTTP 200) but the model's response could not be turned into the
    /// requested JSON shape even after a stricter retry — a prose preamble/refusal, or an
    /// unparseable/truncated body. This is specific to THIS request's content, not a sign
    /// the API is down: retrying the same input reproduces it (a poison pill). Batch callers
    /// must fail just this item and CONTINUE the batch; they must NOT halt or un-count the
    /// attempt (doing so pins the bad item at the queue head forever, stalling everything
    /// behind it). On-demand callers should fall back / surface a retryable error per case.
    /// </summary>
    BadResponse,
}

public class LlmException : Exception
{
    public string? RawResponse { get; }

    /// <summary>
    /// Whether the LLM was unavailable by design (fall back) or a live call failed (bail).
    /// Defaults to <see cref="LlmFailureKind.Unavailable"/> so any unclassified throw keeps
    /// the historical fall-back-to-heuristic behavior; only genuine call failures opt into
    /// <see cref="LlmFailureKind.CallFailed"/>.
    /// </summary>
    public LlmFailureKind Kind { get; }

    public LlmException(
        string message,
        string? rawResponse = null,
        Exception? inner = null,
        LlmFailureKind kind = LlmFailureKind.Unavailable)
        : base(message, inner)
    {
        RawResponse = rawResponse;
        Kind = kind;
    }
}
