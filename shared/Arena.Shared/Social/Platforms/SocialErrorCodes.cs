namespace Arena.Shared.Social.Platforms;

/// <summary>
/// Canonical error codes returned in <see cref="PublishResult.ErrorCode"/>. The publisher's
/// resilience layer (§4.4) classifies these into retryable vs terminal.
/// </summary>
public static class SocialErrorCodes
{
    // --- Terminal (non-retryable): mark Failed immediately, no retry ---
    public const string LengthExceeded = "LENGTH_EXCEEDED";
    public const string Malformed = "MALFORMED";
    public const string ContentRejected = "CONTENT_REJECTED";
    public const string AuthInvalid = "AUTH_INVALID";       // permanent revocation
    public const string AuthMissing = "AUTH_MISSING";       // no credentials configured

    // --- Retryable: mark Pending + backoff ---
    public const string RateLimited = "RATE_LIMITED";       // 429
    public const string Upstream5xx = "UPSTREAM_5XX";       // 5xx
    public const string Timeout = "TIMEOUT";
    public const string Network = "NETWORK";

    /// <summary>True for transient failures that should be retried with backoff (§4.4).</summary>
    public static bool IsRetryable(string? code) => code switch
    {
        RateLimited or Upstream5xx or Timeout or Network => true,
        _ => false,
    };

    /// <summary>Auth failures trip the breaker (§4.4 secrets/auth) but are not retried.</summary>
    public static bool IsAuthFailure(string? code) => code is AuthInvalid or AuthMissing;
}
