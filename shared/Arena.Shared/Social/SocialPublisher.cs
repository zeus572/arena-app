using System.Diagnostics;
using Arena.Shared.Social.Platforms;
using Arena.Shared.Social.Resilience;
using Microsoft.Extensions.Logging;

namespace Arena.Shared.Social;

/// <summary>
/// The publish job (SocialPublisher_Spec §2.2, §4.4, §5, §6). Rides the heartbeat — owns no timer.
/// Each run: (1) selects new candidates and persists them as SocialPost rows (Pending or
/// AwaitingReview), then (2) publishes the due queue under per-platform circuit breakers, daily
/// caps, proactive rate-limit checks, retry-with-backoff, idempotency and a wall-clock time-box.
///
/// Resilience is the top priority: this method only READS core content and only WRITES SocialPosts;
/// per-candidate try/catch keeps one failure from blocking others; and the heartbeat wraps the whole
/// call in a swallow (§4.4) so the core platform is never degraded.
/// </summary>
public sealed class SocialPublisher : ISocialPublisher
{
    private readonly ISocialPostStore _store;
    private readonly IHighlightSelector _selector;
    private readonly IPlatformClientRegistry _platforms;
    private readonly CircuitBreakerRegistry _breakers;
    private readonly ICardRenderer _cards;
    private readonly SocialPublisherOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<SocialPublisher> _logger;

    public SocialPublisher(
        ISocialPostStore store,
        IHighlightSelector selector,
        IPlatformClientRegistry platforms,
        CircuitBreakerRegistry breakers,
        ICardRenderer cards,
        SocialPublisherOptions options,
        IClock clock,
        ILogger<SocialPublisher> logger)
    {
        _store = store;
        _selector = selector;
        _platforms = platforms;
        _breakers = breakers;
        _cards = cards;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var budgetMs = _options.PublisherTickBudgetMs;

        var selected = PersistNewCandidates(now);
        var (published, deferred, failed) = await PublishDueQueueAsync(now, sw, budgetMs, ct);

        _logger.LogInformation(
            "SocialPublisher tick: selected={Selected} published={Published} deferred={Deferred} failed={Failed} elapsedMs={Elapsed}",
            selected, published, deferred, failed, sw.ElapsedMilliseconds);
    }

    // ---- Step 1: selection → persistence -----------------------------------

    private int PersistNewCandidates(DateTimeOffset now)
    {
        var candidates = _selector.SelectCandidates(now);
        var count = 0;
        foreach (var c in candidates)
        {
            _store.Add(new SocialPost
            {
                ContentType = c.ContentType,
                ContentId = c.ContentId,
                Platform = c.Platform,
                Status = c.RequiresReview ? SocialPostStatus.AwaitingReview : SocialPostStatus.Pending,
                Text = c.Text,
                HasImage = c.Card is not null,
                PostScore = c.PostScore,
                CreatedAt = now,
            });
            count++;
        }
        if (count > 0) _store.Save();
        return count;
    }

    // ---- Step 2: publish the due queue -------------------------------------

    private async Task<(int published, int deferred, int failed)> PublishDueQueueAsync(
        DateTimeOffset now, Stopwatch sw, int budgetMs, CancellationToken ct)
    {
        // Due = publishable rows not already published, past their retry gate. The status/null
        // predicates translate to SQL on every provider; the retry-gate and ordering use
        // DateTimeOffset, which we evaluate in memory so the query is provider-agnostic
        // (Npgsql translates it; SQLite — the test provider — does not).
        var due = _store.GetPublishable()
            .Where(p => p.NextRetryAt == null || p.NextRetryAt <= now)
            .OrderByDescending(p => p.PostScore)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        var published = 0; var deferred = 0; var failed = 0;
        var publishedThisTick = new Dictionary<string, int>(StringComparer.Ordinal);
        var publishedToday = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var post in due)
        {
            // Time-box (§4.4): stop cleanly if we've blown the shared-thread budget; rest resumes next tick.
            if (sw.ElapsedMilliseconds > budgetMs)
            {
                _logger.LogWarning("SocialPublisher time-box ({Budget}ms) reached; deferring remaining work.", budgetMs);
                deferred += due.Count - (published + failed + deferred);
                break;
            }

            var platform = post.Platform;

            // Idempotency (§4.4): a row already carrying a PlatformPostId is treated as published.
            if (post.PlatformPostId is not null) continue;

            if (!_platforms.TryGet(platform, out var client))
            {
                deferred++; // no adapter for this platform — leave Pending
                continue;
            }

            var breaker = _breakers.Get(platform);

            // Circuit breaker (§4.4): Open platform is skipped, candidates left Pending (NOT Failed).
            if (!breaker.CanAttempt(now)) { deferred++; continue; }

            // Per-tick cap (§2.2 step 6).
            if (publishedThisTick.GetValueOrDefault(platform) >= _options.MaxPostsPerTick) { deferred++; continue; }

            // Proactive daily cap (§4.4): don't fire requests we know exceed the safety rail.
            if (!publishedToday.ContainsKey(platform))
                publishedToday[platform] = CountPublishedToday(platform, now);
            if (publishedToday[platform] >= _options.DailyCapFor(platform)) { deferred++; continue; }

            // Proactive rate-limit (§4.4): defer rather than provoke a 429.
            if (client.GetRateLimitStatus().IsExhausted) { deferred++; continue; }

            try
            {
                var payload = await BuildPayloadAsync(post, ct);
                var result = await client.PublishAsync(payload, ct);

                if (result.Success)
                {
                    post.Status = SocialPostStatus.Published;
                    post.PlatformPostId = result.PlatformPostId;
                    post.PublishedAt = now;
                    post.ErrorCode = null;
                    post.ErrorMessage = null;
                    breaker.RecordSuccess();
                    publishedThisTick[platform] = publishedThisTick.GetValueOrDefault(platform) + 1;
                    publishedToday[platform] = publishedToday[platform] + 1;
                    published++;
                }
                else
                {
                    if (ApplyFailure(post, result.ErrorCode, result.ErrorMessage, breaker, now)) failed++;
                    else deferred++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unexpected: caught per-candidate so it cannot block other candidates (§4.3).
                if (ApplyFailure(post, SocialErrorCodes.Network, ex.Message, breaker, now)) failed++;
                else deferred++;
            }

            _store.Save();
        }

        return (published, deferred, failed);
    }

    /// <summary>Applies a failed publish per §4.4. Returns true if terminally Failed, false if deferred Pending.</summary>
    private bool ApplyFailure(SocialPost post, string? code, string? message, CircuitBreaker breaker, DateTimeOffset now)
    {
        post.ErrorCode = code;
        post.ErrorMessage = message;

        if (SocialErrorCodes.IsAuthFailure(code))
        {
            // Auth: trip the breaker, leave the post Pending (a down platform must not mark good
            // content Failed). It resumes once credentials are fixed.
            breaker.Trip(now, code!, message ?? "auth failure");
            post.Status = SocialPostStatus.Pending;
            return false;
        }

        if (SocialErrorCodes.IsRetryable(code))
        {
            breaker.RecordFailure(now, code, message);
            post.RetryCount++;
            if (post.RetryCount > _options.MaxRetries)
            {
                post.Status = SocialPostStatus.Failed; // exhausted retries
                return true;
            }
            post.Status = SocialPostStatus.Pending;
            post.NextRetryAt = now + Backoff(post.RetryCount);
            return false;
        }

        // Terminal non-retryable (length, malformed, content rejected): Failed immediately, no breaker trip.
        post.Status = SocialPostStatus.Failed;
        return true;
    }

    /// <summary>Exponential backoff with jitter, capped at MaxBackoffMinutes (§4.4).</summary>
    private TimeSpan Backoff(int retryCount)
    {
        var baseSeconds = _options.BackoffBaseSeconds * Math.Pow(2, retryCount - 1);
        var capSeconds = _options.MaxBackoffMinutes * 60.0;
        var jitter = Random.Shared.NextDouble() * _options.BackoffBaseSeconds;
        return TimeSpan.FromSeconds(Math.Min(capSeconds, baseSeconds) + jitter);
    }

    private int CountPublishedToday(string platform, DateTimeOffset now) =>
        _store.CountPublishedToday(platform, now);

    private async Task<SocialPostPayload> BuildPayloadAsync(SocialPost post, CancellationToken ct)
    {
        byte[]? png = null;
        if (post.HasImage)
        {
            var (template, model) = CardFor(post);
            png = await _cards.RenderAsync(template, model, ct);
        }
        var links = LinkExtractor.Extract(post.Text);
        var altText = post.HasImage ? Truncate(post.Text, 280) : null;
        return new SocialPostPayload(post.Text, png, altText, links);
    }

    /// <summary>Deterministic card model derived from the persisted post (§8: card is decorative reinforcement).</summary>
    private static (CardTemplate, CardModel) CardFor(SocialPost post)
    {
        var (template, kicker) = post.ContentType switch
        {
            SocialContentType.CoalitionHighlight => (CardTemplate.CoalitionHighlight, "Common Ground"),
            SocialContentType.DebateHighlight => (CardTemplate.DebateHighlight, "Debate Highlight"),
            SocialContentType.BriefingAnnounce => (CardTemplate.BriefingAnnounce, "Briefing"),
            _ => (CardTemplate.FeaturePost, "Civersify"),
        };
        return (template, new CardModel(kicker, post.Text, "civersify.com"));
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
