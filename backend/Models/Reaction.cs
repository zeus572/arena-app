namespace Arena.API.Models;

public class Reaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? DebateId { get; set; }
    public Debate? Debate { get; set; }
    public Guid? TurnId { get; set; }
    public Turn? Turn { get; set; }
    public string Type { get; set; } = string.Empty; // e.g. "like", "fire", "think"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
