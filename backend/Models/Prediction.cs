namespace Arena.API.Models;

public class Prediction
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PredictedAgentId { get; set; }
    public Agent PredictedAgent { get; set; } = null!;
    public bool? IsCorrect { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
