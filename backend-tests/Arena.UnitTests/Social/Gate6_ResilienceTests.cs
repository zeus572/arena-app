using Arena.API.Models.Social;
using Arena.API.Social;
using Arena.API.Social.Platforms;
using Arena.API.Social.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Arena.UnitTests.Social.SocialPublisherHarness;

namespace Arena.UnitTests.Social;

/// <summary>
/// Gate 6 (SocialPublisher_Spec §9, Phase 6): the ten platform-down assertions. Two fake clients
/// ("bluesky" + a synthetic "fake-2") prove per-platform isolation even though only Bluesky ships.
/// </summary>
public class Gate6_ResilienceTests
{
    // (1) Core unaffected: a throwing publisher never aborts the heartbeat; other work still runs.
    [Fact]
    public async Task Assertion01_publisher_exception_is_swallowed_and_core_work_continues()
    {
        var hook = new SocialHeartbeatHook(
            scopeFactory: null!, new SocialPublisherOptions(), new TestClock(),
            NullLogger<SocialHeartbeatHook>.Instance);
        var throwing = new ThrowingPublisher();

        var coreRan = false;
        var ran = await hook.RunSafelyAsync(throwing, DateTimeOffset.UtcNow, default);
        coreRan = true; // would not be reached if the exception propagated

        ran.Should().BeFalse("the publisher threw");
        coreRan.Should().BeTrue("the tick continued after the swallow");
        throwing.Calls.Should().Be(1);
    }

    // (2) Circuit opens after CircuitFailureThreshold consecutive failures; rest deferred, no calls.
    [Fact]
    public async Task Assertion02_breaker_opens_and_defers_remaining_candidates()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions { MaxPostsPerTick = 10 }; // cap must not mask the breaker
        var fake2 = new FakePlatformClient("fake-2")
        {
            Responder = _ => PublishResult.Fail(SocialErrorCodes.Upstream5xx, "boom"),
        };
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "c1", 0.9, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "c2", 0.8, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "c3", 0.7, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "c4", 0.6, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "c5", 0.5, false, "fake-2"),
            }
        };
        var (pub, breakers, clock, _) = Build(db, selector, options, fake2);

        await pub.RunOnceAsync(clock.Now, default);

        breakers.Get("fake-2").State.Should().Be(CircuitState.Open);
        fake2.CallCount.Should().Be(options.CircuitFailureThreshold,
            "no further calls once the breaker Opens");
        db.SocialPosts.Count(p => p.Status == SocialPostStatus.Failed).Should().Be(0,
            "a down platform leaves candidates Pending, never Failed");
        db.SocialPosts.Count(p => p.Status == SocialPostStatus.Pending).Should().Be(5);
    }

    // (3) Isolation: with fake-2 Open, bluesky still publishes in the same tick.
    [Fact]
    public async Task Assertion03_one_platform_down_does_not_affect_another()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions { MaxPostsPerTick = 10 };
        var bluesky = new FakePlatformClient("bluesky");
        var fake2 = new FakePlatformClient("fake-2")
        {
            Responder = _ => PublishResult.Fail(SocialErrorCodes.Upstream5xx, "down"),
        };
        var blueId = Guid.NewGuid();
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f1", 0.95, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f2", 0.9, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f3", 0.85, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, blueId, "BLUE", 0.5, false, "bluesky"),
            }
        };
        var (pub, breakers, clock, _) = Build(db, selector, options, bluesky, fake2);

        await pub.RunOnceAsync(clock.Now, default);

        breakers.Get("fake-2").State.Should().Be(CircuitState.Open);
        breakers.Get("bluesky").State.Should().Be(CircuitState.Closed);
        bluesky.CallCount.Should().Be(1);
        ByContent(db, blueId)!.Status.Should().Be(SocialPostStatus.Published);
    }

    // (4) Retryable → Pending + RetryCount++ + future NextRetryAt; terminal → Failed immediately.
    [Fact]
    public async Task Assertion04_retryable_defers_terminal_fails()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var retryId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var client = new FakePlatformClient("bluesky")
        {
            Responder = p => p.Text == "RETRY"
                ? PublishResult.Fail(SocialErrorCodes.RateLimited, "429")
                : PublishResult.Fail(SocialErrorCodes.LengthExceeded, "too long"),
        };
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, retryId, "RETRY", 0.9, false),
                Cand(SocialContentType.DebateHighlight, termId, "TERM", 0.8, false),
            }
        };
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        await pub.RunOnceAsync(clock.Now, default);

        var retry = ByContent(db, retryId)!;
        retry.Status.Should().Be(SocialPostStatus.Pending);
        retry.RetryCount.Should().Be(1);
        retry.NextRetryAt.Should().NotBeNull();
        retry.NextRetryAt!.Value.Should().BeAfter(clock.Now);

        var term = ByContent(db, termId)!;
        term.Status.Should().Be(SocialPostStatus.Failed);
        term.RetryCount.Should().Be(0, "terminal errors are not retried");
    }

    // (5) Backoff respected: skipped while NextRetryAt is future; retried once time passes.
    [Fact]
    public async Task Assertion05_backoff_gate_is_respected()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var client = new FakePlatformClient("bluesky");
        var (pub, _, clock, _) = Build(db, new FakeSelector(), new SocialPublisherOptions(), client);

        var future = clock.Now.AddMinutes(10);
        var post = Insert(db, "bluesky", "later", nextRetryAt: future);

        await pub.RunOnceAsync(clock.Now, default);
        client.CallCount.Should().Be(0, "NextRetryAt is in the future");
        ByContent(db, post.ContentId!.Value)!.Status.Should().Be(SocialPostStatus.Pending);

        clock.Advance(TimeSpan.FromMinutes(11));
        await pub.RunOnceAsync(clock.Now, default);
        client.CallCount.Should().Be(1, "the retry time has passed");
        ByContent(db, post.ContentId!.Value)!.Status.Should().Be(SocialPostStatus.Published);
    }

    // (6) Max retries: after MaxRetries the post is Failed and never re-selected.
    [Fact]
    public async Task Assertion06_max_retries_terminates()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions();
        var client = new FakePlatformClient("bluesky")
        {
            Responder = _ => PublishResult.Fail(SocialErrorCodes.Upstream5xx, "5xx"),
        };
        var (pub, _, clock, _) = Build(db, new FakeSelector(), options, client);

        // Already at the retry ceiling: the next failure exhausts it.
        var post = Insert(db, "bluesky", "tired", retryCount: options.MaxRetries);

        await pub.RunOnceAsync(clock.Now, default);
        ByContent(db, post.ContentId!.Value)!.Status.Should().Be(SocialPostStatus.Failed);
        client.CallCount.Should().Be(1);

        await pub.RunOnceAsync(clock.Now, default);
        client.CallCount.Should().Be(1, "a Failed post is never re-selected");
    }

    // (7) Proactive cap/rate: exhausted rate-limit → no call, candidate deferred Pending.
    [Fact]
    public async Task Assertion07_rate_exhaustion_defers_without_calling()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var id = Guid.NewGuid();
        var client = new FakePlatformClient("bluesky")
        {
            RateLimit = new RateLimitStatus(IsExhausted: true, Remaining: 0, ResetAt: null),
        };
        var selector = new FakeSelector
        {
            Candidates = { Cand(SocialContentType.DebateHighlight, id, "x", 0.9, false) }
        };
        var (pub, _, clock, _) = Build(db, selector, new SocialPublisherOptions(), client);

        await pub.RunOnceAsync(clock.Now, default);

        client.CallCount.Should().Be(0, "we do not fire a request we know will 429");
        ByContent(db, id)!.Status.Should().Be(SocialPostStatus.Pending);
    }

    // (8) Idempotency: a row already carrying a PlatformPostId is not re-published.
    [Fact]
    public async Task Assertion08_idempotency_skips_already_published_rows()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var client = new FakePlatformClient("bluesky");
        var (pub, _, clock, _) = Build(db, new FakeSelector(), new SocialPublisherOptions(), client);

        // Ambiguous prior attempt: request sent, response lost — row already carries a PlatformPostId.
        Insert(db, "bluesky", "dupe", status: SocialPostStatus.Pending, platformPostId: "at://already/posted");

        await pub.RunOnceAsync(clock.Now, default);

        client.CallCount.Should().Be(0, "a row with a PlatformPostId is treated as already published");
    }

    // (9) Time-box: work exceeding PublisherTickBudgetMs stops cleanly; the tick still completes.
    [Fact]
    public async Task Assertion09_time_box_stops_cleanly()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions { PublisherTickBudgetMs = 1, MaxPostsPerTick = 10 };
        var client = new FakePlatformClient("bluesky") { Delay = TimeSpan.FromMilliseconds(60) };
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "t1", 0.9, false),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "t2", 0.8, false),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "t3", 0.7, false),
            }
        };
        var (pub, _, clock, _) = Build(db, selector, options, client);

        var act = async () => await pub.RunOnceAsync(clock.Now, default);
        await act.Should().NotThrowAsync("the time-box stops work cleanly");

        client.CallCount.Should().Be(1, "only the first publish fit inside the 1ms budget");
        db.SocialPosts.Count(p => p.Status == SocialPostStatus.Pending).Should().BeGreaterThan(0,
            "the rest are deferred to the next tick");
    }

    // (10) Health endpoint reflects breaker state and per-platform counts after a down scenario.
    [Fact]
    public async Task Assertion10_health_endpoint_reflects_state()
    {
        using var sql = new SqliteTestDb();
        using var db = sql.NewContext();
        var options = new SocialPublisherOptions { MaxPostsPerTick = 10 };
        var bluesky = new FakePlatformClient("bluesky");
        var fake2 = new FakePlatformClient("fake-2")
        {
            Responder = _ => PublishResult.Fail(SocialErrorCodes.Upstream5xx, "down"),
        };
        var blueId = Guid.NewGuid();
        var selector = new FakeSelector
        {
            Candidates =
            {
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f1", 0.95, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f2", 0.9, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, Guid.NewGuid(), "f3", 0.85, false, "fake-2"),
                Cand(SocialContentType.DebateHighlight, blueId, "BLUE", 0.5, false, "bluesky"),
            }
        };
        var (pub, breakers, clock, registry) = Build(db, selector, options, bluesky, fake2);
        await pub.RunOnceAsync(clock.Now, default);

        var health = new SocialHealthService(db, breakers, registry, clock);
        var report = await health.GetAsync(default);

        var fake2Health = report.Platforms.Single(p => p.Platform == "fake-2");
        fake2Health.BreakerState.Should().Be("Open");
        fake2Health.LastErrorCode.Should().Be(SocialErrorCodes.Upstream5xx);

        var blueHealth = report.Platforms.Single(p => p.Platform == "bluesky");
        blueHealth.BreakerState.Should().Be("Closed");
        blueHealth.PublishedToday.Should().Be(1);
    }
}
