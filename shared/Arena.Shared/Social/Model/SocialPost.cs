namespace Arena.Shared.Social;

/// <summary>
/// The four content sources the publisher draws from (SocialPublisher_Spec §2).
/// No LLM is involved in classifying these — they map 1:1 to existing platform content.
/// </summary>
public enum SocialContentType
{
    BriefingAnnounce = 0,
    CoalitionHighlight = 1,
    DebateHighlight = 2,
    FeaturePost = 3,

    // Civic Arena sources (real coalition bills + zeitgeist), distinct from the debate-app
    // CoalitionHighlight (which is a common_ground debate). Separate values so dedup and
    // analytics can tell civic posts apart.
    CivicBillOutcome = 10,
    CivicZeitgeist = 11,
    CivicOpenBill = 12,
}

/// <summary>
/// Lifecycle of a single social post record (SocialPublisher_Spec §5).
/// </summary>
public enum SocialPostStatus
{
    /// <summary>Selected, awaiting a publish attempt (also the resting state for retry-deferred posts).</summary>
    Pending = 0,

    /// <summary>Routed to the human review queue (§6); not published until Approved.</summary>
    AwaitingReview = 1,

    /// <summary>Reviewer approved; re-enters the publish path next tick.</summary>
    Approved = 2,

    /// <summary>Successfully published; <see cref="SocialPost.PlatformPostId"/> is set.</summary>
    Published = 3,

    /// <summary>Terminal failure (non-retryable, or exhausted retries). Never re-selected.</summary>
    Failed = 4,

    /// <summary>Reviewer rejected, or deliberately not published.</summary>
    Skipped = 5,
}

/// <summary>
/// A single (attempted) social media post. This is the publisher's ONLY writable table
/// (§4.4 isolation) and the single source of truth for dedup (§5).
/// </summary>
public class SocialPost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SocialContentType ContentType { get; set; }

    /// <summary>Ref to the source content (debate / coalition / briefing). Null for seeded FeaturePost.</summary>
    public Guid? ContentId { get; set; }

    /// <summary>Platform key, e.g. "bluesky". Other platforms added when their adapters ship.</summary>
    public string Platform { get; set; } = string.Empty;

    public SocialPostStatus Status { get; set; } = SocialPostStatus.Pending;

    public string Text { get; set; } = string.Empty;
    public bool HasImage { get; set; }

    /// <summary>Set on a successful publish; presence also guards idempotency (§4.4).</summary>
    public string? PlatformPostId { get; set; }

    public double PostScore { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }

    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Incremented on each retryable failure (§4.4 retry/backoff).</summary>
    public int RetryCount { get; set; }

    /// <summary>The selector skips this post until this time has passed (backoff gate).</summary>
    public DateTimeOffset? NextRetryAt { get; set; }
}
