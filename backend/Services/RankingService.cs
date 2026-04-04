using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class RankingService
{
    private readonly ILogger<RankingService> _logger;

    public RankingService(ILogger<RankingService> logger)
    {
        _logger = logger;
    }

    public async Task<DebateAggregate> ComputeScoreAsync(ArenaDbContext db, Debate debate)
    {
        var formatConfig = DebateFormatConfig.Get(debate.Format);

        var turns = await db.Turns.Where(t => t.DebateId == debate.Id).ToListAsync();
        var voteCount = await db.Votes.CountAsync(v => v.DebateId == debate.Id);
        var reactionCount = await db.Reactions.CountAsync(r => r.DebateId == debate.Id);

        var proponent = await db.Agents.FindAsync(debate.ProponentId);
        var opponent = await db.Agents.FindAsync(debate.OpponentId);

        // Relevance: static for MVP
        var relevance = 5.0;

        // Quality: based on average turn word count
        var quality = 0.0;
        if (turns.Count > 0)
        {
            var avgWords = turns.Average(t => t.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            quality = Math.Min(10, avgWords / 30.0);
        }

        // Engagement: based on votes and reactions, with format multiplier
        var rawEngagement = Math.Min(10, (voteCount * 2.0 + reactionCount) / 5.0);
        var engagement = rawEngagement * formatConfig.EngagementMultiplier;
        engagement = Math.Min(10, engagement); // cap at 10

        // Diversity: static for MVP
        var diversity = 5.0;

        // Novelty: static for MVP
        var novelty = 5.0;

        // Recency: exponential decay with format-aware half-life
        var hoursAge = (DateTime.UtcNow - debate.CreatedAt).TotalHours;
        var recency = 10.0 * Math.Exp(-hoursAge / formatConfig.RecencyHalfLifeHours);

        // Reputation: average of both agents
        var reputation = 0.0;
        if (proponent is not null && opponent is not null)
        {
            reputation = (proponent.ReputationScore + opponent.ReputationScore) / 2.0;
            reputation = Math.Min(10, reputation / 10.0);
        }

        // Penalties
        var penalties = 0.0;
        if (debate.Status == DebateStatus.Cancelled) penalties += 10;
        if (turns.Count < 2) penalties += 3;

        var totalScore = relevance + quality + engagement + diversity + novelty + recency + reputation - penalties;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await db.DebateAggregates
            .FirstOrDefaultAsync(a => a.DebateId == debate.Id && a.AggregateDate == today);

        if (existing is not null)
        {
            existing.VoteCount = voteCount;
            existing.ReactionCount = reactionCount;
            existing.RelevanceScore = relevance;
            existing.QualityScore = quality;
            existing.EngagementScore = engagement;
            existing.DiversityScore = diversity;
            existing.NoveltyScore = novelty;
            existing.RecencyScore = recency;
            existing.ReputationScore = reputation;
            existing.PenaltyScore = penalties;
            existing.TotalScore = totalScore;
            existing.ComputedAt = DateTime.UtcNow;
            return existing;
        }

        var aggregate = new DebateAggregate
        {
            Id = Guid.NewGuid(),
            DebateId = debate.Id,
            AggregateDate = today,
            VoteCount = voteCount,
            ReactionCount = reactionCount,
            RelevanceScore = relevance,
            QualityScore = quality,
            EngagementScore = engagement,
            DiversityScore = diversity,
            NoveltyScore = novelty,
            RecencyScore = recency,
            ReputationScore = reputation,
            PenaltyScore = penalties,
            TotalScore = totalScore,
        };

        db.DebateAggregates.Add(aggregate);
        return aggregate;
    }
}
