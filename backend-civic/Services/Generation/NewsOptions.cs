namespace Civic.API.Services.Generation;

public class NewsOptions
{
    public int IngestIntervalHours { get; set; } = 2;
    public int GenerationIntervalMinutes { get; set; } = 120;
    public int BatchSize { get; set; } = 5;
    public int MaxItemsPerDay { get; set; } = 10;

    /// <summary>
    /// RSS feed URLs to ingest, keyed by source name. If empty, the ingestion
    /// service is registered but does nothing (handy in tests).
    /// </summary>
    public Dictionary<string, string> Sources { get; set; } = new();

    /// <summary>
    /// Per-locality RSS feeds: outer key is a 2-letter state code (e.g. "WA"),
    /// inner map is sourceName -> feed URL. Items ingested from these feeds are
    /// tagged with the state code so the briefing/coalition pipeline can scope
    /// them to readers in that locality. See <c>Civic.API.Models.Localities</c>.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> LocalSources { get; set; } = new();
}
