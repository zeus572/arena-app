using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arena.Shared.News;

/// <summary>
/// Reads a single RSS/Atom feed and projects entries to <see cref="NewsItem"/>.
/// Tolerant to per-feed transient failures — returns an empty list and logs a
/// warning rather than throwing.
/// </summary>
public class RssNewsSource : INewsSource
{
    private readonly HttpClient _http;
    private readonly Uri _feedUrl;
    private readonly int _maxEntries;
    private readonly int _minTitleLength;
    private readonly ILogger _logger;

    public string Name { get; }

    public RssNewsSource(
        HttpClient http,
        string name,
        Uri feedUrl,
        int maxEntries = 15,
        int minTitleLength = 15,
        ILogger? logger = null)
    {
        _http = http;
        Name = name;
        _feedUrl = feedUrl;
        _maxEntries = maxEntries;
        _minTitleLength = minTitleLength;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            var xml = await _http.GetStringAsync(_feedUrl, ct);
            using var reader = XmlReader.Create(new StringReader(xml));
            var feed = SyndicationFeed.Load(reader);

            var items = new List<NewsItem>();
            foreach (var item in feed.Items.Take(_maxEntries))
            {
                var title = item.Title?.Text?.Trim();
                if (string.IsNullOrEmpty(title) || title.Length < _minTitleLength)
                {
                    continue;
                }

                var publishedAt = item.PublishDate != DateTimeOffset.MinValue
                    ? item.PublishDate.UtcDateTime
                    : DateTime.UtcNow;

                var url = item.Links.FirstOrDefault()?.Uri?.ToString() ?? _feedUrl.ToString();
                var externalId = string.IsNullOrEmpty(item.Id) ? url : item.Id!;
                var summary = item.Summary?.Text;

                items.Add(new NewsItem(externalId, title, Name, url, summary, publishedAt));
            }
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RssNewsSource[{Source}]: fetch from {Url} failed", Name, _feedUrl);
            return Array.Empty<NewsItem>();
        }
    }
}
