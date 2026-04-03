namespace Arena.API.Models;

public enum DebateStatus
{
    Pending,
    Active,
    Completed,
    Cancelled,
    Compromising
}

public class Debate
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DebateStatus Status { get; set; } = DebateStatus.Pending;
    public Guid ProponentId { get; set; }
    public Agent Proponent { get; set; } = null!;
    public Guid OpponentId { get; set; }
    public Agent Opponent { get; set; } = null!;
    /// <summary>"user", "bot", or "breaking" — source of debate creation</summary>
    public string Source { get; set; } = "bot";
    public Guid? GeneratedTopicId { get; set; }
    public GeneratedTopic? GeneratedTopic { get; set; }
    public Guid? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<DebateTag> DebateTags { get; set; } = new List<DebateTag>();
}
