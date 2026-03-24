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

        var items = await joined
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
                TurnCount = x.Debate.Turns.Count,
                VoteCount = x.Debate.Votes.Count,
                ReactionCount = x.Debate.Reactions.Count,
                TotalScore = x.Agg != null ? x.Agg.TotalScore : 0,
            })
            .ToListAsync();

        return Ok(new { items, totalCount });
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
