namespace Arena.API.Models;

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string Persona { get; set; } = string.Empty;
    public double ReputationScore { get; set; }

    // Personality traits (0-10 scale)
    public double Aggressiveness { get; set; } = 5;
    public double Eloquence { get; set; } = 5;
    public double FactReliance { get; set; } = 5;
    public double Empathy { get; set; } = 5;
    public double Wit { get; set; } = 5;

    public bool IsWildcard { get; set; }
    public bool IsCommentator { get; set; }

    /// <summary>"original", "celebrity", or "historical"</summary>
    public string? AgentType { get; set; }
    /// <summary>null for modern, "founding" | "civil-war" | "20th-century" for historical</summary>
    public string? Era { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
    public ICollection<Debate> DebatesAsProponent { get; set; } = new List<Debate>();
    public ICollection<Debate> DebatesAsOpponent { get; set; } = new List<Debate>();
    public ICollection<AgentSource> Sources { get; set; } = new List<AgentSource>();
}
