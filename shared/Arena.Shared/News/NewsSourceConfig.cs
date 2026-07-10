namespace Arena.Shared.News;

/// <summary>
/// Typed descriptor for one configured news source. The source's display name
/// is the dictionary key it is configured under, not a field here. Which fields
/// apply depends on <see cref="Kind"/>; unknown kinds are skipped (with a
/// warning) by <see cref="NewsSourceFactory"/> rather than failing binding.
/// </summary>
public class NewsSourceConfig
{
    /// <summary>Provider kind — see <see cref="NewsSourceKinds"/>.</summary>
    public string Kind { get; set; } = NewsSourceKinds.Rss;

    /// <summary>Disabled sources are skipped without a warning.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Per-source override of the default max entries per fetch (15).</summary>
    public int? MaxEntries { get; set; }

    // ---- Kind = Rss ----

    /// <summary>Absolute feed URL (RSS/Atom).</summary>
    public string? Url { get; set; }

    // ---- Kind = GoogleNews ----

    /// <summary>Which Google News feed shape to query.</summary>
    public GoogleNewsFeedKind Feed { get; set; } = GoogleNewsFeedKind.Top;

    /// <summary>Topic section id (e.g. POLITICS, NATION) for <see cref="GoogleNewsFeedKind.Topic"/>.</summary>
    public string? Topic { get; set; }

    /// <summary>Place name (e.g. "Washington State") for <see cref="GoogleNewsFeedKind.Geo"/>.</summary>
    public string? Location { get; set; }

    /// <summary>Search query for <see cref="GoogleNewsFeedKind.Search"/>.</summary>
    public string? Query { get; set; }
}

/// <summary>
/// Known <see cref="NewsSourceConfig.Kind"/> values. A string (not an enum) so
/// adding a provider doesn't require touching this file — the factory resolves
/// kinds against registered <see cref="INewsSourceBuilder"/>s at runtime.
/// </summary>
public static class NewsSourceKinds
{
    public const string Rss = "Rss";
    public const string GoogleNews = "GoogleNews";
}

public enum GoogleNewsFeedKind
{
    /// <summary>Top headlines for the edition.</summary>
    Top,
    /// <summary>A curated topic section (POLITICS, NATION, ...).</summary>
    Topic,
    /// <summary>Local headlines for a place.</summary>
    Geo,
    /// <summary>Arbitrary search query.</summary>
    Search,
}
