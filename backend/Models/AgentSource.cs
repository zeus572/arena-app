namespace Arena.API.Models;

public enum SourceType
{
    Book,
    Speech,
    Letter,
    PolicyDocument,
    SocialMedia,
    Interview,
    LegalDocument,
    Other
}

public class AgentSource
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public SourceType SourceType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string ExcerptText { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? ThemeTag { get; set; }
    public int Priority { get; set; } = 2;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
