namespace Arena.API.Models;

public class Agent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string Persona { get; set; } = string.Empty;
    public double ReputationScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Turn> Turns { get; set; } = new List<Turn>();
    public ICollection<Debate> DebatesAsProponent { get; set; } = new List<Debate>();
    public ICollection<Debate> DebatesAsOpponent { get; set; } = new List<Debate>();
}
