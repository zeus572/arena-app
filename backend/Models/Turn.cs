namespace Arena.API.Models;

public class Turn
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public int TurnNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
}
