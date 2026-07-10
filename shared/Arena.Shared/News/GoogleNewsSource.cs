using System.ServiceModel.Syndication;
using Microsoft.Extensions.Logging;

namespace Arena.Shared.News;

/// <summary>
/// Google News RSS source (<c>news.google.com/rss</c>): top headlines, curated
/// topic sections, geo/local feeds, or search queries, per
/// <see cref="NewsSourceConfig.Feed"/>. Google News is an aggregator, so each
/// entry carries the real outlet in its RSS <c>&lt;source&gt;</c> element —
/// surfaced as <see cref="NewsItem.Publisher"/>, with the "Headline - Publisher"
/// title suffix stripped. Entry links are news.google.com redirect URLs.
/// </summary>
public class GoogleNewsSource : RssNewsSource
{
    public GoogleNewsSource(
        HttpClient http,
        string name,
        NewsSourceConfig config,
        int maxEntries = 15,
        int minTitleLength = 15,
        ILogger? logger = null)
        : base(http, name, BuildFeedUrl(config), maxEntries, minTitleLength, logger)
    {
    }

    /// <summary>
    /// Builds the feed URL for a config. The edition params (hl/gl/ceid) are
    /// pinned to the US English edition — that's the whole point of adding
    /// this provider (US-centric national/local coverage).
    /// </summary>
    public static Uri BuildFeedUrl(NewsSourceConfig config)
    {
        const string edition = "hl=en-US&gl=US&ceid=US:en";
        var url = config.Feed switch
        {
            GoogleNewsFeedKind.Top => $"https://news.google.com/rss?{edition}",
            GoogleNewsFeedKind.Topic =>
                $"https://news.google.com/rss/headlines/section/topic/{Uri.EscapeDataString(Require(config.Topic, "Topic").ToUpperInvariant())}?{edition}",
            GoogleNewsFeedKind.Geo =>
                $"https://news.google.com/rss/headlines/section/geo/{Uri.EscapeDataString(Require(config.Location, "Location"))}?{edition}",
            GoogleNewsFeedKind.Search =>
                $"https://news.google.com/rss/search?q={Uri.EscapeDataString(Require(config.Query, "Query"))}&{edition}",
            _ => throw new ArgumentOutOfRangeException(nameof(config), config.Feed, "Unknown Google News feed kind"),
        };
        return new Uri(url);
    }

    protected override NewsItem? MapItem(SyndicationItem item)
    {
        var baseItem = base.MapItem(item);
        if (baseItem is null)
        {
            return null;
        }

        // RSS 2.0 <source url="...">Publisher</source> → SyndicationItem.SourceFeed.
        var publisher = item.SourceFeed?.Title?.Text?.Trim();
        if (string.IsNullOrEmpty(publisher))
        {
            publisher = null;
        }

        var headline = StripPublisherSuffix(baseItem.Headline, publisher);
        if (headline.Length < MinTitleLength)
        {
            return null;
        }

        // Google News <description> is an HTML cluster of related-coverage
        // links, not a summary — worse than nothing downstream, so drop it.
        return baseItem with { Headline = headline, Summary = null, Publisher = publisher };
    }

    /// <summary>
    /// Removes a trailing " - Publisher" / " – Publisher" from a Google News
    /// title, only when it matches the entry's actual publisher — never a
    /// blind split on the last dash, which would mangle legitimate headlines.
    /// </summary>
    private static string StripPublisherSuffix(string title, string? publisher)
    {
        if (publisher is null)
        {
            return title;
        }

        foreach (var dash in new[] { " - ", " – " })
        {
            var suffix = dash + publisher;
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return title[..^suffix.Length].TrimEnd();
            }
        }
        return title;
    }

    private static string Require(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"Google News config requires {field} for this feed kind");
}
