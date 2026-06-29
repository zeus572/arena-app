using Arena.API.Data;
using Arena.Shared.Social;
using Microsoft.EntityFrameworkCore;

namespace Arena.API.Social.Selection;

/// <summary>
/// Binds <see cref="IRankingScoreProvider"/> to the existing Ranking Engine output
/// (DebateAggregate rows produced by RankingService / RankingRollupService).
///
/// Pure read of already-computed, already-stored signals — NO model call. For Coalition/Debate
/// content the ContentId is a Debate.Id; the most recent aggregate for that debate is returned.
/// BriefingAnnounce / FeaturePost have no ranking score and yield null (the selector then uses
/// FeaturePostBaseScore).
/// </summary>
public sealed class RankingScoreProvider : IRankingScoreProvider
{
    private readonly ArenaDbContext _db;

    public RankingScoreProvider(ArenaDbContext db) => _db = db;

    public RankingScore? GetScore(SocialContentType type, Guid contentId)
    {
        if (type is not (SocialContentType.DebateHighlight or SocialContentType.CoalitionHighlight))
            return null;

        var agg = _db.DebateAggregates
            .AsNoTracking()
            .Where(a => a.DebateId == contentId)
            .OrderByDescending(a => a.ComputedAt)
            .FirstOrDefault();

        if (agg is null) return null;

        return new RankingScore(
            Relevance: agg.RelevanceScore,
            Quality: agg.QualityScore,
            Engagement: agg.EngagementScore,
            Diversity: agg.DiversityScore,
            Novelty: agg.NoveltyScore,
            Recency: agg.RecencyScore,
            Reputation: agg.ReputationScore,
            Penalties: agg.PenaltyScore);
    }
}
