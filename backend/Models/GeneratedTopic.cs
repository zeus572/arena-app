namespace Arena.API.Models;

public class GeneratedTopic
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = "static"; // "static", "news"
    public bool Used { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // News metadata (populated when Source == "news")
    public string? NewsHeadline { get; set; }
    public string? NewsSource { get; set; }
    public DateTime? NewsPublishedAt { get; set; }
}
