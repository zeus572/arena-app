using Arena.Shared.Social;

namespace Arena.Shared.Social;

// ===========================================================================
// Dependency contracts (SocialPublisher_Spec §3) plus the small supporting
// abstractions this feature needs. Everything here is LLM-free and, on the
// selection/scoring path, side-effect-free.
// ===========================================================================

/// <summary>Ranking Engine score for a piece of content (§3). Components are on the 0..10 ranking scale.</summary>
public sealed record RankingScore(
    double Relevance, double Quality, double Engagement,
    double Diversity, double Novelty, double Recency,
    double Reputation, double Penalties);

/// <summary>Reads existing ranking scores. NEVER computes via a model — pure read of stored signals.</summary>
public interface IRankingScoreProvider
{
    /// <summary>Returns null if no score exists for this content.</summary>
    RankingScore? GetScore(SocialContentType type, Guid contentId);
}

/// <summary>
/// Coalition breadth / bipartisan signal (§2.1). Bound to a real geometry signal if one exists,
/// otherwise the deterministic Values-Profile-axes fallback. MUST be LLM-free and side-effect-free.
/// </summary>
public interface ICoalitionSignalProvider
{
    bool TryGetBreadth(Guid coalitionId, out double breadthNormalized);
    bool IsBipartisan(Guid coalitionId);
}

/// <summary>Optional seeded/admin-authored evergreen FeaturePosts (§2 source 4). Default: none.</summary>
public interface IFeaturePostProvider
{
    IReadOnlyList<FeaturePostSeed> GetDueSeeds(DateTimeOffset now);
}

public sealed record FeaturePostSeed(Guid Id, string Text, IReadOnlyList<string> Links, string? AltText);

/// <summary>A selected, scored, ranked post-worthy item (output of the selector).</summary>
public sealed record PostCandidate
{
    public required SocialContentType ContentType { get; init; }
    public required Guid? ContentId { get; init; }
    public required string Platform { get; init; }
    public required string Text { get; init; }
    public required double PostScore { get; init; }
    public required int Priority { get; init; }
    public required bool RequiresReview { get; init; }
    public IReadOnlyList<string> Links { get; init; } = Array.Empty<string>();
    public string? AltText { get; init; }
    public CardModel? Card { get; init; }

    /// <summary>Optional distinct card-image body. When null the card falls back to <see cref="Text"/>;
    /// a source sets it to make the image something other than a repeat of the post copy
    /// (e.g. the Civic open-bill "Would you rather?" choice).</summary>
    public string? CardBody { get; init; }
}

public interface IHighlightSelector
{
    IReadOnlyList<PostCandidate> SelectCandidates(DateTimeOffset now);
}

// ---- Platform adapter contracts (§3 / §4) ----

public sealed record SocialPostPayload(
    string Text,                                 // already platform-length-valid
    byte[]? ImagePng,                            // optional card
    string? AltText,
    IReadOnlyList<string> Links);

public sealed record PublishResult(
    bool Success, string? PlatformPostId, string? ErrorCode, string? ErrorMessage)
{
    public static PublishResult Ok(string platformPostId) => new(true, platformPostId, null, null);
    public static PublishResult Fail(string code, string message) => new(false, null, code, message);
}

/// <summary>Snapshot of a platform's rate-limit budget so the scheduler can act before hitting 429s (§4.4).</summary>
public sealed record RateLimitStatus(bool IsExhausted, int? Remaining, DateTimeOffset? ResetAt)
{
    public static RateLimitStatus Available { get; } = new(false, null, null);
}

public interface IPlatformClient
{
    string PlatformKey { get; }                 // "bluesky" (only adapter at launch; "x" etc. deferred)
    Task<PublishResult> PublishAsync(SocialPostPayload payload, CancellationToken ct);
    RateLimitStatus GetRateLimitStatus();
}

/// <summary>Resolves platform clients by key. Launch registers only "bluesky".</summary>
public interface IPlatformClientRegistry
{
    bool TryGet(string platformKey, out IPlatformClient client);
    IReadOnlyCollection<string> Keys { get; }
}

// ---- Card rendering (§3 / §8) ----

public enum CardTemplate
{
    BriefingAnnounce = 0,
    CoalitionHighlight = 1,
    DebateHighlight = 2,
    FeaturePost = 3,
}

/// <summary>Data bound into a card template. Purely decorative reinforcement of already-validated copy.</summary>
public sealed record CardModel(
    string Headline,
    string Body,
    string Footer,
    int Width = 1200,
    int Height = 675);

public interface ICardRenderer
{
    Task<byte[]> RenderAsync(CardTemplate template, CardModel model, CancellationToken ct);
}

// ---- Publisher entry point (§3 / §4.4) ----

/// <summary>The heartbeat invokes this once per (downsampled) tick. Owns no timer of its own.</summary>
public interface ISocialPublisher
{
    Task RunOnceAsync(DateTimeOffset now, CancellationToken ct);
}

/// <summary>Injectable clock so resilience timing (backoff, breaker cooldown) is testable.</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}
