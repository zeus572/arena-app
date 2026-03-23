namespace Arena.API.Models;

public class Vote
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid VotedForAgentId { get; set; }
    public Agent VotedForAgent { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
