using Arena.Shared.News;

namespace Civic.API.Services.Generation;

public class NewsOptions
{
    public int IngestIntervalHours { get; set; } = 2;
    public int GenerationIntervalMinutes { get; set; } = 120;
    public int BatchSize { get; set; } = 5;
    public int MaxItemsPerDay { get; set; } = 10;

    /// <summary>
    /// Stories whose <c>PublishedAt</c> is older than this are never turned into
    /// briefings. Caps how stale the feed can get and stops a perpetually-growing
    /// backlog of old, high-volume national items from crowding out fresh and local
    /// stories during selection. See <c>CivicContentGenerationService</c>.
    /// </summary>
    public int MaxStoryAgeDays { get; set; } = 14;

    /// <summary>
    /// An incoming item whose headline matches (case-insensitive) one ingested
    /// within this many days is skipped as a duplicate — aggregator channels
    /// (Google News) re-surface stories the direct feeds already delivered, and
    /// overlap each other within a tick. 0 disables headline dedupe.
    /// </summary>
    public int HeadlineDedupeWindowDays { get; set; } = 3;

    /// <summary>
    /// National sources to ingest, keyed by source name (the name becomes
    /// <c>NewsItem.Source</c> and its own selection bucket). Each entry is a
    /// typed descriptor whose <c>Kind</c> picks the provider — see
    /// <see cref="NewsSourceConfig"/>. If empty, the ingestion service is
    /// registered but does nothing (handy in tests).
    /// </summary>
    public Dictionary<string, NewsSourceConfig> Sources { get; set; } = new();

    /// <summary>
    /// Per-locality sources: outer key is a 2-letter state code (e.g. "WA"),
    /// inner map is sourceName -> descriptor. Items ingested from these feeds
    /// are tagged with the state code so the briefing/coalition pipeline can
    /// scope them to readers in that locality. See <c>Civic.API.Models.Localities</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, NewsSourceConfig>> LocalSources { get; set; } = new();
}
