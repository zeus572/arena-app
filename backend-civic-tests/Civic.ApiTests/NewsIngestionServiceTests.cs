using System.Linq;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Generation;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Arena.Shared.News;
using WireNewsItem = Arena.Shared.News.NewsItem;

namespace Civic.ApiTests;

[Collection("Database")]
public class NewsIngestionServiceTests
{
    private readonly DatabaseFixture _fx;

    public NewsIngestionServiceTests(DatabaseFixture fx) => _fx = fx;

    private (NewsIngestionService Svc, InMemoryNewsFeed Feed) Build(NewsOptions? options = null)
    {
        var feed = new InMemoryNewsFeed();
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var httpFactory = _fx.Factory.Services.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = _fx.Factory.Services.GetRequiredService<ILoggerFactory>();
        var sourceFactory = new NewsSourceFactory(
            new INewsSourceBuilder[]
            {
                new RssSourceBuilder(httpFactory, loggerFactory),
                new GoogleNewsSourceBuilder(httpFactory, loggerFactory),
            },
            loggerFactory);
        var opts = Options.Create(options ?? new NewsOptions { IngestIntervalHours = 24 });
        var svc = new NewsIngestionService(
            scopes,
            feed,
            new TestOptionsMonitor<NewsOptions>(opts.Value),
            sourceFactory,
            NullLogger<NewsIngestionService>.Instance,
            _fx.Factory.Services.GetRequiredService<Civic.API.Services.StartupReadiness>());
        return (svc, feed);
    }

    [Fact]
    public async Task IngestOnce_PersistsNewItems()
    {
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-1", "Story one breaks news", "TEST", "https://example.com/1", null, DateTime.UtcNow.AddHours(-1)),
            new WireNewsItem("ext-2", "Story two has details", "TEST", "https://example.com/2", "summary", DateTime.UtcNow.AddHours(-2)),
        };

        var added = await svc.IngestOnceAsync();

        added.Should().Be(2);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var rows = await db.NewsItems.OrderBy(n => n.ExternalId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Status == NewsItemStatus.Ingested);
        rows.Should().OnlyContain(r => r.PublishedAt.Kind == DateTimeKind.Utc);
        rows.Should().OnlyContain(r => r.Locality == null, "the injected feed is the national feed");
    }

    [Fact]
    public async Task IngestOnce_IsIdempotent_BySameExternalId()
    {
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-dup", "Repeat headline appears across runs", "TEST", "https://example.com/dup", null, DateTime.UtcNow),
        };

        var first = await svc.IngestOnceAsync();
        var second = await svc.IngestOnceAsync();

        first.Should().Be(1);
        second.Should().Be(0, "the same external id should not insert again");
    }

    [Fact]
    public async Task IngestOnce_EmptyFeed_DoesNothing()
    {
        await _fx.ResetMutableAsync();
        var (svc, _) = Build();
        var added = await svc.IngestOnceAsync();
        added.Should().Be(0);
    }

    [Fact]
    public async Task IngestOnce_OverlongSummary_IsTruncatedAndPersisted()
    {
        // Regression: full-content RSS feeds (e.g. WA's Cascade PBS) emit a
        // <description> longer than the Summary varchar(2000) column. Before the
        // clamp, SaveChanges threw Postgres 22001 and aborted the whole tick, so
        // no local news ever persisted. The item must now survive, clipped to 2000.
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        var hugeSummary = new string('x', 5000);
        feed.Items = new[]
        {
            new WireNewsItem("ext-huge", "Story with a giant body", "TEST", "https://example.com/huge", hugeSummary, DateTime.UtcNow),
        };

        var added = await svc.IngestOnceAsync();

        added.Should().Be(1, "an over-long summary must not abort ingestion");
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var row = await db.NewsItems.SingleAsync(n => n.ExternalId == "ext-huge");
        row.Summary.Should().NotBeNull();
        row.Summary!.Length.Should().Be(2000, "the summary should be clamped to the column limit");
    }

    [Fact]
    public async Task IngestOnce_PersistsPublisher_ClampedToColumnLimit()
    {
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-pub", "City council debates a new zoning ordinance", "Google News", "https://news.google.com/x", null, DateTime.UtcNow)
                { Publisher = "NPR" },
            new WireNewsItem("ext-pub-long", "Supreme court hears the redistricting appeal", "Google News", "https://news.google.com/y", null, DateTime.UtcNow)
                { Publisher = new string('p', 300) },
        };

        var added = await svc.IngestOnceAsync();

        added.Should().Be(2);
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.NewsItems.SingleAsync(n => n.ExternalId == "ext-pub")).Publisher.Should().Be("NPR");
        (await db.NewsItems.SingleAsync(n => n.ExternalId == "ext-pub-long")).Publisher!.Length
            .Should().Be(120, "the publisher should be clamped to the column limit");
    }

    [Fact]
    public async Task IngestOnce_SkipsDuplicateHeadline_WithinWindow()
    {
        // Aggregator channels re-surface stories the direct feeds already
        // delivered — different ExternalIds, same headline. Within the dedupe
        // window only the first copy is kept (case-insensitively).
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-npr", "Senate passes the big bill", "NPR", "https://npr.org/big-bill", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(1);

        feed.Items = new[]
        {
            new WireNewsItem("ext-gn", "SENATE PASSES THE BIG BILL", "Google News", "https://news.google.com/big-bill", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(0, "the same headline within the window is a duplicate even with a new external id");
    }

    [Fact]
    public async Task IngestOnce_CollapsesNearDuplicateHeadlines_KeepingDeepestStory()
    {
        // A big breaking story carried by several outlets in the same tick with
        // slightly different wording. Exact-headline dedupe missed these (no two
        // are identical), so each became its own briefing. They must now collapse
        // to a single row — the one with the most in-depth summary.
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-npr", "Senator Lindsey Graham dies at 70", "NPR",
                "https://npr.org/graham", "A brief wire note.", DateTime.UtcNow),
            new WireNewsItem("ext-bbc", "Lindsey Graham, longtime South Carolina senator, dead at 70", "BBC",
                "https://bbc.com/graham",
                new string('x', 900) /* the fullest write-up — deepest story */, DateTime.UtcNow),
            new WireNewsItem("ext-ap", "Graham, veteran senator from South Carolina, has died", "AP",
                "https://ap.org/graham", "Medium-length recap of the news.", DateTime.UtcNow),
        };

        var added = await svc.IngestOnceAsync();

        added.Should().Be(1, "the three outlets cover one story, so only one row survives");
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var rows = await db.NewsItems.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].ExternalId.Should().Be("ext-bbc", "the outlet with the deepest (longest) summary wins the cluster");
    }

    [Fact]
    public async Task IngestOnce_DistinctStoriesSharingAnActor_AreBothKept()
    {
        // Two genuinely different stories that merely share a proper noun must not
        // be collapsed — dedupe requires substantial word overlap, not one name.
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-a", "Senate passes the annual defense budget", "NPR", "https://npr.org/a", null, DateTime.UtcNow),
            new WireNewsItem("ext-b", "Senate confirms a new ambassador to France", "NPR", "https://npr.org/b", null, DateTime.UtcNow),
        };

        var added = await svc.IngestOnceAsync();

        added.Should().Be(2, "sharing only the word 'Senate' does not make two stories duplicates");
    }

    [Fact]
    public async Task IngestOnce_NearDuplicateAcrossTicks_IsSkipped()
    {
        // The reworded copy arrives a tick later (a new aggregator channel picks
        // it up). The look-back against stored rows must still recognise it.
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build();
        feed.Items = new[]
        {
            new WireNewsItem("ext-first", "Governor signs the sweeping new climate law", "NPR", "https://npr.org/climate", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(1);

        feed.Items = new[]
        {
            new WireNewsItem("ext-second", "Governor signs sweeping climate law into effect", "Google News", "https://news.google.com/climate", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(0, "a near-duplicate of a recently stored headline is a duplicate");
    }

    [Fact]
    public async Task IngestOnce_DuplicateHeadline_OutsideWindow_IsIngested()
    {
        await _fx.ResetMutableAsync();
        var (svc, feed) = Build(new NewsOptions { IngestIntervalHours = 24, HeadlineDedupeWindowDays = 3 });
        feed.Items = new[]
        {
            new WireNewsItem("ext-old", "Recurring headline about the budget", "NPR", "https://npr.org/budget", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(1);

        // Age the stored copy out of the dedupe window.
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var row = await db.NewsItems.SingleAsync(n => n.ExternalId == "ext-old");
            row.IngestedAt = DateTime.UtcNow.AddDays(-4);
            await db.SaveChangesAsync();
        }

        feed.Items = new[]
        {
            new WireNewsItem("ext-new", "Recurring headline about the budget", "Google News", "https://news.google.com/budget", null, DateTime.UtcNow),
        };
        (await svc.IngestOnceAsync()).Should().Be(1, "an old copy outside the window no longer blocks re-ingestion");
    }
}

internal class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
