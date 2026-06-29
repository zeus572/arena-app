using Arena.API.Data;
using Arena.API.Models;
using Arena.Shared.Social;
using Microsoft.EntityFrameworkCore;

namespace Arena.API.Social.Selection;

/// <summary>
/// Selects post-worthy content from already-generated, already-scored data (SocialPublisher_Spec §2).
///
/// HARD RULE: no LLM and no network on this path. Scores come from <see cref="IRankingScoreProvider"/>
/// (a read of stored DebateAggregate rows); coalition breadth comes from
/// <see cref="ICoalitionSignalProvider"/> (deterministic). Any caption text is deterministic string
/// logic, never a model call.
/// </summary>
public sealed class HighlightSelector : IHighlightSelector
{
    private readonly ArenaDbContext _db;
    private readonly IRankingScoreProvider _ranking;
    private readonly ICoalitionSignalProvider _coalition;
    private readonly IFeaturePostProvider _featurePosts;
    private readonly IPlatformClientRegistry _platforms;
    private readonly SocialPublisherOptions _options;

    public HighlightSelector(
        ArenaDbContext db,
        IRankingScoreProvider ranking,
        ICoalitionSignalProvider coalition,
        IFeaturePostProvider featurePosts,
        IPlatformClientRegistry platforms,
        SocialPublisherOptions options)
    {
        _db = db;
        _ranking = ranking;
        _coalition = coalition;
        _featurePosts = featurePosts;
        _platforms = platforms;
        _options = options;
    }

    // Priorities per §2 table (lower = higher priority).
    private const int PriorityCoalition = 1;
    private const int PriorityBriefing = 2;
    private const int PriorityDebate = 3;
    private const int PriorityFeature = 4;

    public IReadOnlyList<PostCandidate> SelectCandidates(DateTimeOffset now)
    {
        var platformKeys = _platforms.Keys.Count > 0
            ? _platforms.Keys.ToArray()
            : new[] { "bluesky" }; // launch default if no registry populated

        var since = now.UtcDateTime.AddHours(-_options.LookbackHours);

        // Pull completed debates in the window once; classify into coalition / debate-highlight.
        var debates = _db.Debates
            .AsNoTracking()
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .Where(d => d.Status == DebateStatus.Completed && d.UpdatedAt >= since)
            .ToList();

        var raw = new List<PostCandidate>();
        var coalitionDebateIds = new HashSet<Guid>();

        // ---- Source 1: CoalitionHighlight (priority 1) ----
        foreach (var d in debates.Where(d => d.Format == _options.CoalitionFormat))
        {
            // Candidacy: coalition reached (Completed) + passes bipartisan-breadth check (§2.1).
            if (!_coalition.TryGetBreadth(d.Id, out var breadth)) continue;
            if (breadth < _options.CoalitionBreadthMin) continue;
            if (!_coalition.IsBipartisan(d.Id)) continue;

            coalitionDebateIds.Add(d.Id);
            var score = ScoreFromRanking(SocialContentType.CoalitionHighlight, d.Id);
            AddPerPlatform(raw, platformKeys, SocialContentType.CoalitionHighlight, d.Id,
                PriorityCoalition, score, InvolvesRealFigure(d),
                text: $"Common ground found — {d.Topic}",
                card: new CardModel("Common Ground", d.Topic, "Civersify"));
        }

        // ---- Source 2: BriefingAnnounce (priority 2) ----
        // DISCOVERY: no backend Briefing entity exists (frontend-civic prototype only). This source
        // is intentionally empty until a Briefing read model is bound. FLAGGED for Sam (§3 stub).
        foreach (var c in GatherBriefingCandidates(now)) raw.Add(c);

        // ---- Source 3: DebateHighlight (priority 3) ----
        foreach (var d in debates.Where(d =>
                     _options.DebateHighlightFormats.Contains(d.Format) &&
                     !coalitionDebateIds.Contains(d.Id))) // a coalition debate is published as a coalition, not twice
        {
            var rank = _ranking.GetScore(SocialContentType.DebateHighlight, d.Id);
            var engagement = rank is null ? 0 : Normalize(rank.Engagement);
            if (engagement < _options.DebateEngagementMin) continue; // §2 candidacy

            var score = ScoreFromRanking(SocialContentType.DebateHighlight, d.Id);
            AddPerPlatform(raw, platformKeys, SocialContentType.DebateHighlight, d.Id,
                PriorityDebate, score, InvolvesRealFigure(d),
                text: d.Topic,
                card: new CardModel("Debate Highlight", d.Topic, "Civersify"));
        }

        // ---- Source 4: FeaturePost (priority 4) ----
        foreach (var seed in _featurePosts.GetDueSeeds(now))
        {
            // No ranking score: fixed baseline (§2 PostScore note). ContentId stays null (§5).
            var score = _options.FeaturePostBaseScore;
            foreach (var platform in platformKeys)
            {
                raw.Add(new PostCandidate
                {
                    ContentType = SocialContentType.FeaturePost,
                    ContentId = null,
                    Platform = platform,
                    Text = seed.Text,
                    Links = seed.Links,
                    AltText = seed.AltText,
                    PostScore = score,
                    Priority = PriorityFeature,
                    RequiresReview = RequiresReview(SocialContentType.FeaturePost, score, penaltiesNormalized: 0, involvesRealFigure: false),
                    Card = new CardModel("Civersify", seed.Text, "Civersify"),
                });
            }
        }

        // Dedup (§2 step 3): drop any (ContentType, ContentId, Platform) already in SocialPosts.
        var deduped = ExcludeAlreadyPosted(raw);

        // Sort by (Priority asc, PostScore desc); stable tiebreak for determinism.
        return deduped
            .OrderBy(c => c.Priority)
            .ThenByDescending(c => c.PostScore)
            .ThenBy(c => c.ContentId ?? Guid.Empty)
            .ThenBy(c => c.Platform, StringComparer.Ordinal)
            .ToList();
    }

    private void AddPerPlatform(
        List<PostCandidate> sink, string[] platformKeys, SocialContentType type, Guid contentId,
        int priority, double score, bool involvesRealFigure, string text, CardModel card)
    {
        var penaltiesNorm = PenaltiesNormalized(type, contentId);
        foreach (var platform in platformKeys)
        {
            sink.Add(new PostCandidate
            {
                ContentType = type,
                ContentId = contentId,
                Platform = platform,
                Text = text,
                PostScore = score,
                Priority = priority,
                RequiresReview = RequiresReview(type, score, penaltiesNorm, involvesRealFigure),
                Card = card,
            });
        }
    }

    /// <summary>PostScore from ranking signals (§2). Normalized weighted sum minus normalized penalties.</summary>
    private double ScoreFromRanking(SocialContentType type, Guid contentId)
    {
        var s = _ranking.GetScore(type, contentId);
        if (s is null) return _options.FeaturePostBaseScore; // no score → baseline fallback
        return _options.WQuality * Normalize(s.Quality)
             + _options.WEngagement * Normalize(s.Engagement)
             + _options.WNovelty * Normalize(s.Novelty)
             + _options.WRecency * Normalize(s.Recency)
             - Normalize(s.Penalties);
    }

    private double PenaltiesNormalized(SocialContentType type, Guid contentId)
    {
        var s = _ranking.GetScore(type, contentId);
        return s is null ? 0 : Normalize(s.Penalties);
    }

    private double Normalize(double rawComponent) => rawComponent / _options.RankingComponentMax;

    /// <summary>
    /// §2.2 step 7 + §6: route to review when a real political figure is involved, OR the score is
    /// below the auto-publish floor, OR the underlying content carries a high penalty.
    /// </summary>
    private bool RequiresReview(SocialContentType type, double postScore, double penaltiesNormalized, bool involvesRealFigure)
    {
        if (involvesRealFigure &&
            type is SocialContentType.CoalitionHighlight or SocialContentType.DebateHighlight)
            return true;
        if (postScore < _options.AutoPublishMin) return true;
        if (penaltiesNormalized > _options.ReviewPenaltyThreshold) return true;
        return false;
    }

    /// <summary>Real-world political figures are celebrity/historical agents (Agent.AgentType). Synthetic ("original") publish directly.</summary>
    private static bool InvolvesRealFigure(Debate d) =>
        IsRealFigure(d.Proponent) || IsRealFigure(d.Opponent);

    private static bool IsRealFigure(Agent? a) =>
        a?.AgentType is "celebrity" or "historical";

    /// <summary>Placeholder source — no backend Briefing entity exists yet (see §2 note above).</summary>
    private static IEnumerable<PostCandidate> GatherBriefingCandidates(DateTimeOffset now)
        => Array.Empty<PostCandidate>();

    private List<PostCandidate> ExcludeAlreadyPosted(List<PostCandidate> candidates)
    {
        var ids = candidates.Where(c => c.ContentId.HasValue).Select(c => c.ContentId!.Value).Distinct().ToList();
        if (ids.Count == 0) return candidates;

        var posted = _db.SocialPosts
            .AsNoTracking()
            .Where(p => p.ContentId != null && ids.Contains(p.ContentId.Value))
            .Select(p => new { p.ContentType, p.ContentId, p.Platform })
            .ToList()
            .Select(p => (p.ContentType, p.ContentId!.Value, p.Platform))
            .ToHashSet();

        return candidates
            .Where(c => !c.ContentId.HasValue ||
                        !posted.Contains((c.ContentType, c.ContentId.Value, c.Platform)))
            .ToList();
    }
}

/// <summary>Default FeaturePost source: none. Real deployments seed admin posts via config/DB.</summary>
public sealed class EmptyFeaturePostProvider : IFeaturePostProvider
{
    public IReadOnlyList<FeaturePostSeed> GetDueSeeds(DateTimeOffset now) => Array.Empty<FeaturePostSeed>();
}
