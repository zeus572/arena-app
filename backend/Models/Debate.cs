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
    /// <summary>"standard", "common_ground", "tweet", "rapid_fire", "longform", "roast", "town_hall"</summary>
    public string Format { get; set; } = "standard";
    /// <summary>"user", "bot", or "breaking" — source of debate creation</summary>
    public string Source { get; set; } = "bot";
    public Guid? GeneratedTopicId { get; set; }
    public GeneratedTopic? GeneratedTopic { get; set; }
    public Guid? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public Guid? ArenaId { get; set; }
    public DebateArena? Arena { get; set; }
    /// <summary>If this debate was forked from another, the parent debate's Id.</summary>
    public Guid? ForkedFromDebateId { get; set; }
    public Debate? ForkedFromDebate { get; set; }
    /// <summary>Optional notes the forker attached — "what changed" in this fork.</summary>
    public string? ForkNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<DebateTag> DebateTags { get; set; } = new List<DebateTag>();
    public ICollection<DebateParticipant> Participants { get; set; } = new List<DebateParticipant>();
    public ICollection<Debate> Forks { get; set; } = new List<Debate>();
}
