namespace Arena.API.Models;

public enum TurnType
{
    Argument,
    Arbiter,
    Compromise,
    Wildcard,
    Commentary,
    Agreement,
    Question,
    Roast
}

public class Turn
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public int TurnNumber { get; set; }
    public TurnType Type { get; set; } = TurnType.Argument;
    public string Content { get; set; } = string.Empty;
    /// <summary>JSON array of citations: [{ "source", "title", "url" }]</summary>
    public string? CitationsJson { get; set; }
    /// <summary>JSON object with argument breakdown: { claims, evidence, assumptions, weaknesses }</summary>
    public string? AnalysisJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
}
