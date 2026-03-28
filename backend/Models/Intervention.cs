namespace Arena.API.Models;

public class Intervention
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>User-submitted question or prompt for the agents</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>Number of upvotes from other users</summary>
    public int Upvotes { get; set; }
    /// <summary>Whether this intervention was injected into a turn</summary>
    public bool Used { get; set; }
    /// <summary>Which turn consumed this intervention</summary>
    public int? UsedInTurnNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
