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
    private readonly StartupReadiness _readiness;

    public NewsIngestionService(
        IServiceScopeFactory scopes,
        INewsFeed feed,
        IOptionsMonitor<NewsOptions> opts,
        IHttpClientFactory httpFactory,
        ILoggerFactory loggerFactory,
        ILogger<NewsIngestionService> log,
        StartupReadiness readiness)
    {
        _scopes = scopes;
        _feed = feed;
        _opts = opts;
        _httpFactory = httpFactory;
        _loggerFactory = loggerFactory;
        _log = log;
        _readiness = readiness;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Park until DB migrations + seeding finish so we never touch an un-migrated schema.
        try { await _readiness.WaitUntilReadyAsync(stoppingToken); }
        catch (OperationCanceledException) { return; }

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

        // Each feed is ingested in its own try/catch so one failing feed (a flaky
        // RSS source, or an item that trips a DB constraint) can't starve the
        // others. Before this guard, a single oversized WA item aborted the whole
        // tick, taking national + MD + CA down with it.
        var total = await SafeIngestAsync(_feed, locality: null, db, ct);

        foreach (var (state, sources) in _opts.CurrentValue.LocalSources)
        {
            var feed = BuildLocalFeed(state, sources);
            if (feed is null) continue;
            total += await SafeIngestAsync(feed, locality: state, db, ct);
        }

        return total;
    }

    /// <summary>
    /// Ingest a single feed, swallowing and logging any failure so the caller can
    /// continue with the remaining feeds. A failed feed contributes 0 fresh items.
    /// EF tracked entities from the failed SaveChanges are detached so they don't
    /// re-poison the next feed's save on the shared <paramref name="db"/>.
    /// </summary>
    private async Task<int> SafeIngestAsync(INewsFeed feed, string? locality, CivicDbContext db, CancellationToken ct)
    {
        var label = locality ?? "national";
        try
        {
            return await IngestFeedAsync(feed, locality, db, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "NewsIngestionService: {Label} feed ingestion failed; skipping to next feed", label);
            DetachPending(db);
            return 0;
        }
    }

    /// <summary>Detach any entities still pending after a failed save so they don't leak into the next feed's SaveChanges.</summary>
    private static void DetachPending(CivicDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToList())
            entry.State = EntityState.Detached;
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
                // Clamp every string field to its column limit. RSS feeds vary
                // wildly — some put the full article body in <description> — and
                // an over-long value otherwise throws Postgres 22001 on save,
                // aborting the feed. Summary is a preview, so clipping is benign.
                ExternalId = Clamp(i.ExternalId, NewsFieldLimits.ExternalId)!,
                Headline = Clamp(i.Headline, NewsFieldLimits.Headline)!,
                Source = Clamp(i.Source, NewsFieldLimits.Source)!,
                Url = Clamp(i.Url, NewsFieldLimits.Url)!,
                Summary = Clamp(i.Summary, NewsFieldLimits.Summary),
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

        // Metric: how long are the raw summaries, and how much are we clipping?
        // Logged as structured fields so the trend (truncated rate, longest seen)
        // is visible over time in the log store without a DB query.
        var summaryLengths = fresh
            .Select(f => items.First(i => i.ExternalId == f.ExternalId).Summary?.Length ?? 0)
            .ToList();
        var truncated = summaryLengths.Count(len => len > NewsFieldLimits.Summary);
        var longest = summaryLengths.Count > 0 ? summaryLengths.Max() : 0;

        db.NewsItems.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        _log.LogInformation(
            "NewsIngestionService: {Label} — ingested {Added} new items; summaries: longest {Longest} chars, {Truncated}/{Added} truncated to {Limit}",
            label, fresh.Count, longest, truncated, fresh.Count, NewsFieldLimits.Summary);
        return fresh.Count;
    }

    /// <summary>Trim a string to <paramref name="max"/> chars; null/short values pass through unchanged.</summary>
    private static string? Clamp(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}

/// <summary>
/// String-column limits for <see cref="DbNewsItem"/>, mirroring the
/// <c>[MaxLength]</c> attributes on the model. Kept here so ingestion can clamp
/// inbound RSS values before they hit Postgres. Update both together.
/// </summary>
internal static class NewsFieldLimits
{
    public const int ExternalId = 600;
    public const int Headline = 400;
    public const int Source = 60;
    public const int Url = 800;
    public const int Summary = 2000;
}
