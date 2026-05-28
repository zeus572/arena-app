using System.Linq;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Generation;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using WireNewsItem = Arena.Shared.News.NewsItem;

namespace Civic.ApiTests;

[Collection("Database")]
public class NewsIngestionServiceTests
{
    private readonly DatabaseFixture _fx;

    public NewsIngestionServiceTests(DatabaseFixture fx) => _fx = fx;

    private (NewsIngestionService Svc, InMemoryNewsFeed Feed) Build()
    {
        var feed = new InMemoryNewsFeed();
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var opts = Options.Create(new NewsOptions { IngestIntervalHours = 24 });
        var svc = new NewsIngestionService(
            scopes,
            feed,
            new TestOptionsMonitor<NewsOptions>(opts.Value),
            NullLogger<NewsIngestionService>.Instance);
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
}

internal class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
