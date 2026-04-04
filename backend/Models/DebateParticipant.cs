namespace Arena.API.Models;

public class DebateParticipant
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public string Role { get; set; } = "questioner";
    public int QuestionOrder { get; set; }
}
