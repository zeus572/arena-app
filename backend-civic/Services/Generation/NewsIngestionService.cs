using Arena.Shared.News;
using Civic.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DbNewsItem = Civic.API.Models.NewsItem;
using NewsItemStatus = Civic.API.Models.NewsItemStatus;

namespace Civic.API.Services.Generation;

/// <summary>
/// Periodically pulls news from <see cref="INewsFeed"/> and upserts
/// <see cref="NewsItem"/> rows in civic DB. Idempotent by ExternalId — repeat
/// ingestion of the same headline is a no-op. Does not call Claude.
///
/// The injected <see cref="INewsFeed"/> is the national feed (items tagged
/// Locality=null). Per-locality feeds from <see cref="NewsOptions.LocalSources"/>
/// are built on the fly here (reusing <see cref="RssNewsSource"/>) and their
/// items are tagged with the state code.
/// </summary>
public class NewsIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly INewsFeed _feed;
    private readonly IOptionsMonitor<NewsOptions> _opts;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<NewsIngestionService> _log;

    public NewsIngestionService(
        IServiceScopeFactory scopes,
        INewsFeed feed,
        IOptionsMonitor<NewsOptions> opts,
        IHttpClientFactory httpFactory,
        ILoggerFactory loggerFactory,
        ILogger<NewsIngestionService> log)
    {
        _scopes = scopes;
        _feed = feed;
        _opts = opts;
        _httpFactory = httpFactory;
        _loggerFactory = loggerFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First tick after a short delay so app startup isn't blocked by RSS network.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IngestOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "NewsIngestionService: tick failed");
            }

            var interval = TimeSpan.FromHours(Math.Max(1, _opts.CurrentValue.IngestIntervalHours));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException) { /* shutdown */ }
        }
    }

    /// <summary>
    /// Public so tests can drive a single tick deterministically. Ingests the
    /// national feed plus every configured per-locality feed, and returns the
    /// total number of fresh items inserted across all of them.
    /// </summary>
    public async Task<int> IngestOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var total = await IngestFeedAsync(_feed, locality: null, db, ct);

        foreach (var (state, sources) in _opts.CurrentValue.LocalSources)
        {
            var feed = BuildLocalFeed(state, sources);
            if (feed is null) continue;
            total += await IngestFeedAsync(feed, locality: state, db, ct);
        }

        return total;
    }

    /// <summary>Build an aggregate feed for one locality from its configured RSS sources.</summary>
    private INewsFeed? BuildLocalFeed(string state, Dictionary<string, string> sources)
    {
        var rssSources = new List<INewsSource>();
        foreach (var (name, url) in sources)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                _log.LogWarning("NewsIngestionService: skipping invalid local source {State}/{Name}: {Url}", state, name, url);
                continue;
            }
            rssSources.Add(new RssNewsSource(
                _httpFactory.CreateClient("RssNewsSource"),
                name,
                uri,
                logger: _loggerFactory.CreateLogger($"RssNewsSource[{state}:{name}]")));
        }

        if (rssSources.Count == 0) return null;
        return new AggregateNewsFeed(rssSources, _loggerFactory.CreateLogger<AggregateNewsFeed>());
    }

    /// <summary>
    /// Fetch a single feed and upsert its items, tagging each fresh row with
    /// <paramref name="locality"/> (null for national). Returns fresh count.
    /// </summary>
    private async Task<int> IngestFeedAsync(INewsFeed feed, string? locality, CivicDbContext db, CancellationToken ct)
    {
        var label = locality ?? "national";
        var items = await feed.FetchAsync(maxItems: 30, ct);
        if (items.Count == 0)
        {
            _log.LogInformation("NewsIngestionService: {Label} feed returned no items", label);
            return 0;
        }

        var externalIds = items.Select(i => i.ExternalId).ToList();
        var existing = await db.NewsItems
            .Where(n => externalIds.Contains(n.ExternalId))
            .Select(n => n.ExternalId)
            .ToListAsync(ct);

        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = items
            .Where(i => !existingSet.Contains(i.ExternalId))
            .Select(i => new DbNewsItem
            {
                Id = Guid.NewGuid(),
                ExternalId = i.ExternalId,
                Headline = i.Headline,
                Source = i.Source,
                Url = i.Url,
                Summary = i.Summary,
                PublishedAt = DateTime.SpecifyKind(i.PublishedAt, DateTimeKind.Utc),
                IngestedAt = DateTime.UtcNow,
                Status = NewsItemStatus.Ingested,
                Locality = locality,
            })
            .ToList();

        if (fresh.Count == 0)
        {
            _log.LogInformation("NewsIngestionService: {Label} — 0 new items (all {Total} already ingested)", label, items.Count);
            return 0;
        }

        db.NewsItems.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        _log.LogInformation("NewsIngestionService: {Label} — ingested {Added} new items", label, fresh.Count);
        return fresh.Count;
    }
}
