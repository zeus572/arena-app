using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class FeedController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public FeedController(ArenaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetFeed([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var debates = await _db.Debates
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .GroupJoin(
                _db.DebateAggregates.Where(a => a.AggregateDate == today),
                d => d.Id,
                a => a.DebateId,
                (d, aggs) => new { Debate = d, Agg = aggs.FirstOrDefault() })
            .OrderByDescending(x => x.Agg != null ? x.Agg.TotalScore : 0)
            .ThenByDescending(x => x.Debate.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Debate.Id,
                x.Debate.Topic,
                x.Debate.Description,
                Status = x.Debate.Status.ToString(),
                Proponent = new { x.Debate.Proponent.Id, x.Debate.Proponent.Name, x.Debate.Proponent.AvatarUrl },
                Opponent = new { x.Debate.Opponent.Id, x.Debate.Opponent.Name, x.Debate.Opponent.AvatarUrl },
                x.Debate.CreatedAt,
                TurnCount = x.Debate.Turns.Count,
                VoteCount = x.Debate.Votes.Count,
                ReactionCount = x.Debate.Reactions.Count,
                TotalScore = x.Agg != null ? x.Agg.TotalScore : 0,
            })
            .ToListAsync();

        return Ok(debates);
    }
}
