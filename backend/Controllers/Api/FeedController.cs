using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class FeedController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public FeedController(ArenaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? tag = null,
        [FromQuery] string sort = "hot",
        [FromQuery] string? status = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.Debates
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .AsQueryable();

        // Keyword search
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(d =>
                EF.Functions.ILike(d.Topic, $"%{q}%") ||
                EF.Functions.ILike(d.Description ?? "", $"%{q}%"));
        }

        // Tag filter
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagLower = tag.ToLowerInvariant();
            query = query.Where(d => d.DebateTags.Any(dt => dt.Tag.Name == tagLower));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DebateStatus>(status, true, out var statusEnum))
        {
            query = query.Where(d => d.Status == statusEnum);
        }

        var totalCount = await query.CountAsync();

        // Join with aggregates for scoring
        var joined = query.GroupJoin(
            _db.DebateAggregates.Where(a => a.AggregateDate == today),
            d => d.Id,
            a => a.DebateId,
            (d, aggs) => new { Debate = d, Agg = aggs.FirstOrDefault() });

        // Sort
        joined = sort.ToLowerInvariant() switch
        {
            "new" => joined
                .OrderByDescending(x => x.Debate.CreatedAt),
            "top" => joined
                .OrderByDescending(x => x.Agg != null ? x.Agg.EngagementScore : 0)
                .ThenByDescending(x => x.Debate.CreatedAt),
            _ => joined // "hot" — active first, then by TotalScore
                .OrderByDescending(x => x.Debate.Status == DebateStatus.Active || x.Debate.Status == DebateStatus.Compromising ? 1 : 0)
                .ThenByDescending(x => x.Agg != null ? x.Agg.TotalScore : 0)
                .ThenByDescending(x => x.Debate.UpdatedAt),
        };

        var rawItems = await joined
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Debate.Id,
                x.Debate.Topic,
                x.Debate.Description,
                Status = x.Debate.Status.ToString(),
                Proponent = new { x.Debate.Proponent.Id, x.Debate.Proponent.Name, x.Debate.Proponent.AvatarUrl, x.Debate.Proponent.Persona },
                Opponent = new { x.Debate.Opponent.Id, x.Debate.Opponent.Name, x.Debate.Opponent.AvatarUrl, x.Debate.Opponent.Persona },
                x.Debate.CreatedAt,
                x.Debate.Format,
                x.Debate.Source,
                NewsHeadline = x.Debate.GeneratedTopic != null ? x.Debate.GeneratedTopic.NewsHeadline : null,
                NewsSource = x.Debate.GeneratedTopic != null ? x.Debate.GeneratedTopic.NewsSource : null,
                NewsPublishedAt = x.Debate.GeneratedTopic != null ? x.Debate.GeneratedTopic.NewsPublishedAt : null,
                TurnCount = x.Debate.Turns.Count,
                VoteCount = x.Debate.Votes.Count,
                ReactionCount = x.Debate.Reactions.Count,
                TotalScore = x.Agg != null ? x.Agg.TotalScore : 0,
                ProponentVotes = x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.ProponentId),
                OpponentVotes = x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.OpponentId),
                Reactions = x.Debate.Reactions
                    .GroupBy(r => r.Type)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToList(),
                // Pull candidate "aha-moment" turns: argument-style turns with substantive
                // content, ranked by aha-style reactions then total reactions. We don't
                // gate on reactions because early debates have few — we still want a
                // quote to surface; reaction counts just bump it up the ranking.
                QuoteCandidates = x.Debate.Turns
                    .Where(t => t.Type != TurnType.Arbiter && t.Type != TurnType.Commentary
                        && t.Content != null && t.Content.Length > 60)
                    .OrderByDescending(t => t.Reactions.Count(r =>
                        r.Type == "insightful" || r.Type == "fire" || r.Type == "savage" || r.Type == "surprising"))
                    .ThenByDescending(t => t.Reactions.Count)
                    .ThenBy(t => t.TurnNumber)
                    .Take(3)
                    .Select(t => new QuoteCandidate(
                        t.Content,
                        t.Agent.Name,
                        t.AgentId == x.Debate.ProponentId,
                        t.Reactions.Count,
                        t.Reactions.Count(r =>
                            r.Type == "insightful" || r.Type == "fire" || r.Type == "savage" || r.Type == "surprising")
                    ))
                    .ToList(),
            })
            .ToListAsync();

        // Compute rivalry info for agent pairs on this page
        var agentPairs = rawItems
            .Select(x => (ProId: x.Proponent.Id, OppId: x.Opponent.Id))
            .Distinct()
            .ToList();
        var agentIds = agentPairs.SelectMany(p => new[] { p.ProId, p.OppId }).Distinct().ToList();
        var completedMatchups = await _db.Debates
            .Where(d => d.Status == DebateStatus.Completed
                && agentIds.Contains(d.ProponentId) && agentIds.Contains(d.OpponentId))
            .Include(d => d.Votes)
            .Select(d => new { d.ProponentId, d.OpponentId, Votes = d.Votes.Select(v => v.VotedForAgentId).ToList() })
            .ToListAsync();

        object? GetRivalry(Guid proId, Guid oppId)
        {
            var matchups = completedMatchups
                .Where(d => (d.ProponentId == proId && d.OpponentId == oppId) || (d.ProponentId == oppId && d.OpponentId == proId))
                .ToList();
            if (matchups.Count < 2) return null;
            var proWins = 0;
            var oppWins = 0;
            foreach (var m in matchups)
            {
                var pv = m.Votes.Count(v => v == m.ProponentId);
                var ov = m.Votes.Count(v => v == m.OpponentId);
                var thisProIsOurPro = m.ProponentId == proId;
                if (pv > ov) { if (thisProIsOurPro) proWins++; else oppWins++; }
                else if (ov > pv) { if (thisProIsOurPro) oppWins++; else proWins++; }
            }
            return new { Matchups = matchups.Count, ProponentWins = proWins, OpponentWins = oppWins };
        }

        var items = rawItems.Select(x =>
        {
            var total = x.Reactions.Sum(r => r.Count);
            var reactions = x.Reactions.ToDictionary(r => r.Type, r => r.Count);
            var topQuote = ExtractTopQuote(x.QuoteCandidates);
            return new
            {
                x.Id, x.Topic, x.Description, x.Status, x.Format,
                x.Proponent, x.Opponent, x.CreatedAt, x.Source,
                NewsInfo = x.Source == "breaking" && x.NewsHeadline != null
                    ? new { Headline = x.NewsHeadline, Source = x.NewsSource, PublishedAt = x.NewsPublishedAt }
                    : null,
                x.TurnCount, x.VoteCount, x.ReactionCount, x.TotalScore,
                x.ProponentVotes, x.OpponentVotes,
                Reactions = reactions,
                Label = ComputeLabel(reactions, total, x.ProponentVotes, x.OpponentVotes),
                Rivalry = GetRivalry(x.Proponent.Id, x.Opponent.Id),
                TopQuote = topQuote,
            };
        });

        return Ok(new { items, totalCount });
    }

    private record QuoteCandidate(string Content, string AgentName, bool IsProponent, int ReactionCount, int InsightfulCount);

    private static readonly Regex BoldRegex = new(@"\*\*([^*\n]{15,200})\*\*", RegexOptions.Compiled);
    private static readonly Regex MarkdownNoise = new(@"[*_`>\[\]\(\)]+|^[\s\-•]+", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Pull a punchy "aha moment" quote from a turn's content. Prefers the first
    /// bolded fragment (authors emphasize the punchline themselves), falls back to
    /// the first complete sentence trimmed to a tweet-length window. Skips quotes
    /// that look like list markers or quoted citations.
    /// </summary>
    private static object? ExtractTopQuote(IEnumerable<QuoteCandidate> candidates)
    {
        foreach (var c in candidates)
        {
            var content = c.Content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) continue;

            string? quote = null;

            // Prefer bolded text — debaters emphasize their punchlines.
            var bold = BoldRegex.Match(content);
            if (bold.Success)
            {
                var b = bold.Groups[1].Value.Trim();
                if (b.Length >= 20 && b.Length <= 220 && !b.StartsWith("Total"))
                {
                    quote = b;
                }
            }

            // Fall back to the first impactful sentence.
            if (quote == null)
            {
                var cleaned = MarkdownNoise.Replace(content, " ").Trim();
                var sentences = Regex.Split(cleaned, @"(?<=[\.!\?])\s+");
                foreach (var raw in sentences)
                {
                    var s = raw.Trim();
                    if (s.Length < 30 || s.Length > 240) continue;
                    if (s.StartsWith("http")) continue;
                    quote = s;
                    break;
                }
            }

            if (quote == null) continue;

            quote = Regex.Replace(quote, @"\s+", " ").Trim().TrimEnd(',', ';', ':');
            if (quote.Length < 20) continue;
            if (quote.Length > 220) quote = quote.Substring(0, 217).TrimEnd() + "…";

            return new
            {
                Text = quote,
                c.AgentName,
                c.IsProponent,
                c.ReactionCount,
                c.InsightfulCount,
            };
        }
        return null;
    }

    private static string? ComputeLabel(Dictionary<string, int> reactions, int totalReactions, int proVotes, int oppVotes)
    {
        var totalVotes = proVotes + oppVotes;

        // Controversial: close vote split (40-60% range) with enough votes
        if (totalVotes >= 4)
        {
            var ratio = (double)Math.Min(proVotes, oppVotes) / totalVotes;
            if (ratio >= 0.35) return "Controversial";
        }

        // Insightful: high insightful reaction ratio
        if (totalReactions >= 3)
        {
            var insightful = reactions.GetValueOrDefault("insightful", 0);
            if ((double)insightful / totalReactions >= 0.4) return "Insightful";
        }

        // Heated: high disagree ratio
        if (totalReactions >= 3)
        {
            var disagree = reactions.GetValueOrDefault("disagree", 0);
            if ((double)disagree / totalReactions >= 0.4) return "Heated";
        }

        return null;
    }

    [HttpGet("trending")]
    public async Task<IActionResult> GetTrendingTopics([FromQuery] int limit = 10)
    {
        var trending = await _db.Tags
            .Where(t => t.UsageCount > 0)
            .OrderByDescending(t => t.UsageCount)
            .Take(limit)
            .Select(t => new { Topic = t.DisplayName, Score = t.UsageCount })
            .ToListAsync();

        return Ok(trending);
    }
}
