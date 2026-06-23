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
    /// A live API call WAS attempted and failed — out of credits, rate-limited (429),
    /// overloaded (529), a 5xx, or an unparseable response. The key is configured and
    /// spend was incurred, so a heuristic fallback here would persist low-quality "dead"
    /// data into prod. Synthesis/birth operations should BAIL (skip + retry later) rather
    /// than fall back.
    /// </summary>
    CallFailed,
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
