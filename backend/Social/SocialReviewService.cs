using Arena.API.Data;
using Arena.API.Models.Social;
using Arena.API.Social.Resilience;
using Microsoft.EntityFrameworkCore;

namespace Arena.API.Social;

public sealed record ReviewItem(
    Guid Id, SocialContentType ContentType, Guid? ContentId, string Platform,
    string Text, bool HasImage, double PostScore, DateTimeOffset CreatedAt, string ImagePreviewUrl);

/// <summary>Human review queue operations (SocialPublisher_Spec §6). Minimal API, no full UI this build.</summary>
public sealed class SocialReviewService
{
    private readonly ArenaDbContext _db;
    private readonly IClock _clock;

    public SocialReviewService(ArenaDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ReviewItem>> ListAwaitingReviewAsync(CancellationToken ct) =>
        await _db.SocialPosts
            .Where(p => p.Status == SocialPostStatus.AwaitingReview)
            .OrderByDescending(p => p.PostScore)
            .Select(p => new ReviewItem(
                p.Id, p.ContentType, p.ContentId, p.Platform, p.Text, p.HasImage, p.PostScore, p.CreatedAt,
                $"/api/social/review/{p.Id}/image"))
            .ToListAsync(ct);

    /// <summary>Approve → re-enters the publish path next tick (subject to the same length/rate checks).</summary>
    public async Task<bool> ApproveAsync(Guid id, string reviewedBy, CancellationToken ct)
    {
        var post = await _db.SocialPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null || post.Status != SocialPostStatus.AwaitingReview) return false;
        post.Status = SocialPostStatus.Approved;
        post.ReviewedBy = reviewedBy;
        post.ReviewedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectAsync(Guid id, string reviewedBy, CancellationToken ct)
    {
        var post = await _db.SocialPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null || post.Status != SocialPostStatus.AwaitingReview) return false;
        post.Status = SocialPostStatus.Skipped;
        post.ReviewedBy = reviewedBy;
        post.ReviewedAt = _clock.Now;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record PlatformHealth(
    string Platform, string BreakerState, string? LastErrorCode, string? LastErrorMessage,
    DateTimeOffset? OpenedAt, int PublishedToday, int FailedToday);

public sealed record SocialHealthReport(DateTimeOffset Now, IReadOnlyList<PlatformHealth> Platforms);

/// <summary>Backs GET /api/social/health (§4.4 observability): per-platform breaker state + counts.</summary>
public sealed class SocialHealthService
{
    private readonly ArenaDbContext _db;
    private readonly CircuitBreakerRegistry _breakers;
    private readonly IPlatformClientRegistry _platforms;
    private readonly IClock _clock;

    public SocialHealthService(ArenaDbContext db, CircuitBreakerRegistry breakers,
        IPlatformClientRegistry platforms, IClock clock)
    {
        _db = db;
        _breakers = breakers;
        _platforms = platforms;
        _clock = clock;
    }

    public async Task<SocialHealthReport> GetAsync(CancellationToken ct)
    {
        var now = _clock.Now;
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        // Union of platforms that have a client and platforms that have a breaker (so a down platform shows).
        var keys = _platforms.Keys.Union(_breakers.All.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        // Pull the small per-platform Published/Failed sets with translatable predicates, then
        // count by day in memory (DateTimeOffset comparison is not translated by all providers).
        var recent = await _db.SocialPosts
            .Where(p => p.Status == SocialPostStatus.Published || p.Status == SocialPostStatus.Failed)
            .ToListAsync(ct);

        var items = new List<PlatformHealth>();
        foreach (var platform in keys)
        {
            var publishedToday = recent.Count(p =>
                p.Platform == platform && p.Status == SocialPostStatus.Published &&
                p.PublishedAt != null && p.PublishedAt >= dayStart);
            var failedToday = recent.Count(p =>
                p.Platform == platform && p.Status == SocialPostStatus.Failed &&
                p.CreatedAt >= dayStart);

            var breaker = _breakers.All.TryGetValue(platform, out var b) ? b : null;
            items.Add(new PlatformHealth(
                platform,
                breaker?.State.ToString() ?? CircuitState.Closed.ToString(),
                breaker?.LastErrorCode,
                breaker?.LastErrorMessage,
                breaker?.OpenedAt,
                publishedToday,
                failedToday));
        }

        return new SocialHealthReport(now, items);
    }
}
