namespace Arena.Shared.News;

public interface INewsSource
{
    /// <summary>
    /// Name of this source (e.g. "NPR", "BBC"). Surfaces on emitted NewsItems
    /// and is what consumers use to filter or group.
    /// </summary>
    string Name { get; }

    Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken ct = default);
}

public interface INewsFeed
{
    /// <summary>
    /// Fetches across all registered sources, dedupes by headline (case-insensitive),
    /// and returns up to <paramref name="maxItems"/> items in random order.
    /// </summary>
    Task<IReadOnlyList<NewsItem>> FetchAsync(int maxItems = 30, CancellationToken ct = default);
}
