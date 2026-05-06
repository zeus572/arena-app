namespace Arena.API.Models;

/// <summary>
/// A topic-scoped community space — like a subreddit but for AI debate.
/// Each arena has its own theme, tone, debate format, and rules that get
/// passed into the LLM prompt so generated turns honor the arena's style.
/// Class is named DebateArena to avoid a clash with the root "Arena" namespace.
/// </summary>
public class DebateArena
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Tone { get; set; } = "serious";
    public string Rules { get; set; } = string.Empty;
    public string DefaultFormat { get; set; } = "standard";
    public string IconEmoji { get; set; } = "🏛️";
    public string AccentColor { get; set; } = "#6366f1";
    public bool IsOfficial { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Debate> Debates { get; set; } = new List<Debate>();
}
