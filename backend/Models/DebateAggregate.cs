namespace Arena.API.Models;

public class DebateAggregate
{
    public Guid Id { get; set; }
    public Guid DebateId { get; set; }
    public Debate Debate { get; set; } = null!;
    public DateOnly AggregateDate { get; set; }
    public int VoteCount { get; set; }
    public int ReactionCount { get; set; }
    public int ViewCount { get; set; }
    public double RelevanceScore { get; set; }
    public double QualityScore { get; set; }
    public double EngagementScore { get; set; }
    public double DiversityScore { get; set; }
    public double NoveltyScore { get; set; }
    public double RecencyScore { get; set; }
    public double ReputationScore { get; set; }
    public double PenaltyScore { get; set; }
    public double TotalScore { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
