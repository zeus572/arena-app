using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arena.Shared.News;

/// <summary>
/// Default <see cref="INewsFeed"/>: fetches across every registered
/// <see cref="INewsSource"/> in parallel and dedupes by headline.
/// </summary>
public class AggregateNewsFeed : INewsFeed
{
    private readonly IEnumerable<INewsSource> _sources;
    private readonly ILogger _logger;

    public AggregateNewsFeed(IEnumerable<INewsSource> sources, ILogger<AggregateNewsFeed>? logger = null)
    {
        _sources = sources;
        _logger = logger ?? NullLogger<AggregateNewsFeed>.Instance;
    }

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(int maxItems = 30, CancellationToken ct = default)
    {
        // Each source is guarded individually: a source that throws (in-house
        // ones catch internally, but that's a convention, not a contract) must
        // not fault the whole aggregate and starve the healthy sources.
        var fetches = _sources.Select(s => FetchSafeAsync(s, ct)).ToArray();
        var batches = await Task.WhenAll(fetches);

        var all = batches.SelectMany(b => b).ToList();

        var deduped = all
            .GroupBy(h => h.Headline, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(_ => Random.Shared.Next())
            .Take(maxItems)
            .ToList();

        _logger.LogInformation(
            "AggregateNewsFeed: fetched {Raw} items across {Sources} sources, {Deduped} after dedup",
            all.Count, fetches.Length, deduped.Count);

        return deduped;
    }

    private async Task<IReadOnlyList<NewsItem>> FetchSafeAsync(INewsSource source, CancellationToken ct)
    {
        try
        {
            return await source.FetchAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AggregateNewsFeed: source {Source} threw; treating as empty", source.Name);
            return Array.Empty<NewsItem>();
        }
    }
}
