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
}
