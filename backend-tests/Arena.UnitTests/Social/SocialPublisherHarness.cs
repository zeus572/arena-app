using Arena.API.Data;
using Arena.Shared.Social;
using Arena.Shared.Social.Platforms;
using Arena.Shared.Social.Rendering;
using Arena.Shared.Social.Resilience;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arena.UnitTests.Social;

/// <summary>Shared wiring for the Phase 5/6 publisher integration gates.</summary>
internal static class SocialPublisherHarness
{
    public static (SocialPublisher pub, CircuitBreakerRegistry breakers, TestClock clock, PlatformClientRegistry registry)
        Build(ArenaDbContext db, IHighlightSelector selector, SocialPublisherOptions options, params IPlatformClient[] clients)
    {
        if (clients.Length == 0) clients = new IPlatformClient[] { new FakePlatformClient() };
        var breakers = new CircuitBreakerRegistry(options);
        var registry = new PlatformClientRegistry(clients);
        var clock = new TestClock();
        var renderer = new HtmlCardRenderer(new SolidColorPngRasterizer());
        var pub = new SocialPublisher(new EfSocialPostStore(db), selector, registry, breakers, renderer, options, clock,
            NullLogger<SocialPublisher>.Instance);
        return (pub, breakers, clock, registry);
    }

    public static PostCandidate Cand(SocialContentType type, Guid? id, string text, double score,
        bool review, string platform = "bluesky") => new()
    {
        ContentType = type, ContentId = id, Platform = platform, Text = text,
        PostScore = score, Priority = 3, RequiresReview = review,
    };

    public static SocialPost? ByContent(ArenaDbContext db, Guid contentId) =>
        db.SocialPosts.SingleOrDefault(p => p.ContentId == contentId);

    public static SocialPost Insert(ArenaDbContext db, string platform, string text,
        SocialPostStatus status = SocialPostStatus.Pending, double score = 0.9,
        int retryCount = 0, DateTimeOffset? nextRetryAt = null, string? platformPostId = null,
        Guid? contentId = null)
    {
        var post = new SocialPost
        {
            ContentType = SocialContentType.DebateHighlight,
            ContentId = contentId ?? Guid.NewGuid(),
            Platform = platform,
            Status = status,
            Text = text,
            PostScore = score,
            RetryCount = retryCount,
            NextRetryAt = nextRetryAt,
            PlatformPostId = platformPostId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.SocialPosts.Add(post);
        db.SaveChanges();
        return post;
    }
}
