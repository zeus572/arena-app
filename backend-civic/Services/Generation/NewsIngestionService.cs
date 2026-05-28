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
/// </summary>
public class NewsIngestionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly INewsFeed _feed;
    private readonly IOptionsMonitor<NewsOptions> _opts;
    private readonly ILogger<NewsIngestionService> _log;

    public NewsIngestionService(
        IServiceScopeFactory scopes,
        INewsFeed feed,
        IOptionsMonitor<NewsOptions> opts,
        ILogger<NewsIngestionService> log)
    {
        _scopes = scopes;
        _feed = feed;
        _opts = opts;
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
    /// Public so tests can drive a single tick deterministically.
    /// </summary>
    public async Task<int> IngestOnceAsync(CancellationToken ct = default)
    {
        var items = await _feed.FetchAsync(maxItems: 30, ct);
        if (items.Count == 0)
        {
            _log.LogInformation("NewsIngestionService: feed returned no items");
            return 0;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

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
            })
            .ToList();

        if (fresh.Count == 0)
        {
            _log.LogInformation("NewsIngestionService: 0 new items (all {Total} already ingested)", items.Count);
            return 0;
        }

        db.NewsItems.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        _log.LogInformation("NewsIngestionService: ingested {Added} new items", fresh.Count);
        return fresh.Count;
    }
}
