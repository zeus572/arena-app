using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ArenasController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public ArenasController(ArenaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var arenas = await _db.Arenas
            .OrderByDescending(a => a.IsOfficial)
            .ThenBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.Slug,
                a.Name,
                a.Description,
                a.Topic,
                a.Tone,
                a.DefaultFormat,
                a.IconEmoji,
                a.AccentColor,
                a.IsOfficial,
                DebateCount = a.Debates.Count,
                ActiveDebateCount = a.Debates.Count(d =>
                    d.Status == DebateStatus.Active || d.Status == DebateStatus.Compromising),
            })
            .ToListAsync();

        return Ok(arenas);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var arena = await _db.Arenas.FirstOrDefaultAsync(a => a.Slug == slug);
        if (arena is null) return NotFound();

        var debateCount = await _db.Debates.CountAsync(d => d.ArenaId == arena.Id);
        var activeCount = await _db.Debates.CountAsync(d =>
            d.ArenaId == arena.Id &&
            (d.Status == DebateStatus.Active || d.Status == DebateStatus.Compromising));

        return Ok(new
        {
            arena.Id,
            arena.Slug,
            arena.Name,
            arena.Description,
            arena.Topic,
            arena.Tone,
            arena.Rules,
            arena.DefaultFormat,
            arena.IconEmoji,
            arena.AccentColor,
            arena.IsOfficial,
            arena.CreatedAt,
            DebateCount = debateCount,
            ActiveDebateCount = activeCount,
        });
    }

    [HttpGet("{slug}/feed")]
    public async Task<IActionResult> GetArenaFeed(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "hot")
    {
        var arena = await _db.Arenas.FirstOrDefaultAsync(a => a.Slug == slug);
        if (arena is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.Debates
            .Where(d => d.ArenaId == arena.Id)
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .AsQueryable();

        var totalCount = await query.CountAsync();

        var joined = query.GroupJoin(
            _db.DebateAggregates.Where(a => a.AggregateDate == today),
            d => d.Id,
            a => a.DebateId,
            (d, aggs) => new { Debate = d, Agg = aggs.FirstOrDefault() });

        joined = sort.ToLowerInvariant() switch
        {
            "new" => joined.OrderByDescending(x => x.Debate.CreatedAt),
            "top" => joined.OrderByDescending(x => x.Agg != null ? x.Agg.EngagementScore : 0)
                           .ThenByDescending(x => x.Debate.CreatedAt),
            "controversial" => joined
                .Where(x => x.Debate.Votes.Count >= 2)
                .OrderByDescending(x => x.Debate.Votes.Count > 0
                    ? (double)Math.Min(
                        x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.ProponentId),
                        x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.OpponentId))
                      / x.Debate.Votes.Count
                    : 0)
                .ThenByDescending(x => x.Debate.Votes.Count),
            _ => joined
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
                x.Debate.Format,
                x.Debate.Source,
                Proponent = new { x.Debate.Proponent.Id, x.Debate.Proponent.Name, x.Debate.Proponent.AvatarUrl, x.Debate.Proponent.Persona },
                Opponent = new { x.Debate.Opponent.Id, x.Debate.Opponent.Name, x.Debate.Opponent.AvatarUrl, x.Debate.Opponent.Persona },
                x.Debate.CreatedAt,
                TurnCount = x.Debate.Turns.Count,
                VoteCount = x.Debate.Votes.Count,
                ReactionCount = x.Debate.Reactions.Count,
                TotalScore = x.Agg != null ? x.Agg.TotalScore : 0,
                ProponentVotes = x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.ProponentId),
                OpponentVotes = x.Debate.Votes.Count(v => v.VotedForAgentId == x.Debate.OpponentId),
                ForkCount = x.Debate.Forks.Count,
                IsForked = x.Debate.ForkedFromDebateId != null,
            })
            .ToListAsync();

        return Ok(new
        {
            arena = new
            {
                arena.Id, arena.Slug, arena.Name, arena.Description,
                arena.Topic, arena.Tone, arena.Rules, arena.DefaultFormat,
                arena.IconEmoji, arena.AccentColor,
            },
            items,
            totalCount,
        });
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateArenaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Slug and Name are required." });

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _db.Arenas.AnyAsync(a => a.Slug == slug))
            return Conflict(new { error = $"Arena with slug '{slug}' already exists." });

        if (!DebateFormatConfig.All.ContainsKey(request.DefaultFormat))
            return BadRequest(new { error = $"Invalid format: {request.DefaultFormat}" });

        var arena = new DebateArena
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Topic = request.Topic.Trim(),
            Tone = request.Tone,
            Rules = request.Rules,
            DefaultFormat = request.DefaultFormat,
            IconEmoji = string.IsNullOrWhiteSpace(request.IconEmoji) ? "🏛️" : request.IconEmoji,
            AccentColor = request.AccentColor,
        };

        _db.Arenas.Add(arena);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBySlug), new { slug = arena.Slug }, new { arena.Id, arena.Slug, arena.Name });
    }
}
