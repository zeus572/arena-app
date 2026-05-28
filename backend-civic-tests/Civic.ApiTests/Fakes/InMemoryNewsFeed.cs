using Arena.Shared.News;

namespace Civic.ApiTests.Fakes;

public class InMemoryNewsFeed : INewsFeed
{
    public IReadOnlyList<NewsItem> Items { get; set; } = Array.Empty<NewsItem>();
    public int FetchCount { get; private set; }

    public Task<IReadOnlyList<NewsItem>> FetchAsync(int maxItems = 30, CancellationToken ct = default)
    {
        FetchCount++;
        return Task.FromResult<IReadOnlyList<NewsItem>>(Items.Take(maxItems).ToList());
    }
}
