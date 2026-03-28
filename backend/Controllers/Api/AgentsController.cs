using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public AgentsController(ArenaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var agents = await _db.Agents
            .OrderByDescending(a => a.ReputationScore)
            .ToListAsync();

        // Compute stats for all agents
        var completedDebates = await _db.Debates
            .Where(d => d.Status == DebateStatus.Completed)
            .Include(d => d.Votes)
            .Include(d => d.DebateTags).ThenInclude(dt => dt.Tag)
            .ToListAsync();

        var agentStats = agents.Select(agent =>
        {
            var debates = completedDebates
                .Where(d => d.ProponentId == agent.Id || d.OpponentId == agent.Id)
                .ToList();

            var wins = 0;
            var losses = 0;
            var draws = 0;
            var currentStreak = 0;
            var streakType = "";

            foreach (var debate in debates.OrderBy(d => d.CreatedAt))
            {
                var proVotes = debate.Votes.Count(v => v.VotedForAgentId == debate.ProponentId);
                var oppVotes = debate.Votes.Count(v => v.VotedForAgentId == debate.OpponentId);
                var isProponent = debate.ProponentId == agent.Id;
                var agentVotes = isProponent ? proVotes : oppVotes;
                var opponentVotes = isProponent ? oppVotes : proVotes;

                if (agentVotes > opponentVotes)
                {
                    wins++;
                    if (streakType == "W") currentStreak++;
                    else { currentStreak = 1; streakType = "W"; }
                }
                else if (agentVotes < opponentVotes)
                {
                    losses++;
                    if (streakType == "L") currentStreak++;
                    else { currentStreak = 1; streakType = "L"; }
                }
                else
                {
                    draws++;
                    currentStreak = 0;
                    streakType = "";
                }
            }

            // Top tag from debates
            var topTag = debates
                .SelectMany(d => d.DebateTags)
                .GroupBy(dt => dt.Tag.DisplayName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            // Title
            string? title = null;
            if (currentStreak >= 5 && streakType == "W")
                title = $"Undisputed Champion of {topTag ?? "the Arena"}";
            else if (currentStreak >= 3 && streakType == "W")
                title = $"{currentStreak}-Debate Win Streak";
            else if (wins > 0 && losses == 0 && debates.Count >= 3)
                title = "Undefeated";

            return new
            {
                agent.Id, agent.Name, agent.Description, agent.AvatarUrl,
                agent.Persona, agent.ReputationScore, agent.CreatedAt,
                Stats = new
                {
                    Wins = wins, Losses = losses, Draws = draws,
                    TotalDebates = debates.Count,
                    WinStreak = streakType == "W" ? currentStreak : 0,
                    TopTag = topTag,
                    Title = title,
                },
            };
        });

        return Ok(agentStats);
    }

    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string sort = "top", [FromQuery] string period = "week")
    {
        var now = DateTime.UtcNow;
        var periodStart = period switch
        {
            "day" => now.AddDays(-1),
            "month" => now.AddDays(-30),
            "all" => DateTime.MinValue,
            _ => now.AddDays(-7), // week
        };

        var agents = await _db.Agents.ToListAsync();

        var debates = await _db.Debates
            .Where(d => d.Status == DebateStatus.Completed)
            .Include(d => d.Votes)
            .Include(d => d.Reactions)
            .Include(d => d.Turns).ThenInclude(t => t.Reactions)
            .Include(d => d.DebateTags).ThenInclude(dt => dt.Tag)
            .ToListAsync();

        var periodDebates = debates
            .Where(d => d.CreatedAt >= periodStart)
            .ToList();

        var entries = agents.Select(agent =>
        {
            // All-time stats
            var allDebates = debates
                .Where(d => d.ProponentId == agent.Id || d.OpponentId == agent.Id)
                .ToList();

            // Period stats
            var agentPeriodDebates = periodDebates
                .Where(d => d.ProponentId == agent.Id || d.OpponentId == agent.Id)
                .ToList();

            int wins = 0, losses = 0, draws = 0;
            int periodWins = 0, periodLosses = 0;
            int controversialCount = 0;
            double totalVoteMargin = 0;
            int debatesWithVotes = 0;

            foreach (var d in allDebates)
            {
                var proVotes = d.Votes.Count(v => v.VotedForAgentId == d.ProponentId);
                var oppVotes = d.Votes.Count(v => v.VotedForAgentId == d.OpponentId);
                var isProponent = d.ProponentId == agent.Id;
                var agentVotes = isProponent ? proVotes : oppVotes;
                var opponentVotes = isProponent ? oppVotes : proVotes;
                var totalVotes = proVotes + oppVotes;

                if (agentVotes > opponentVotes) wins++;
                else if (agentVotes < opponentVotes) losses++;
                else draws++;

                // Controversial: close vote splits (35-65 range)
                if (totalVotes >= 4)
                {
                    var pct = (double)agentVotes / totalVotes * 100;
                    if (pct >= 35 && pct <= 65) controversialCount++;
                    totalVoteMargin += Math.Abs(pct - 50);
                    debatesWithVotes++;
                }

                // Period wins
                if (d.CreatedAt >= periodStart)
                {
                    if (agentVotes > opponentVotes) periodWins++;
                    else if (agentVotes < opponentVotes) periodLosses++;
                }
            }

            // Reaction engagement
            var totalReactions = allDebates
                .Where(d => d.ProponentId == agent.Id || d.OpponentId == agent.Id)
                .Sum(d => d.Reactions.Count + d.Turns.Where(t => t.AgentId == agent.Id).Sum(t => t.Reactions.Count));

            var disagreeCount = allDebates
                .SelectMany(d => d.Turns.Where(t => t.AgentId == agent.Id))
                .Sum(t => t.Reactions.Count(r => r.Type == "disagree"));

            var insightfulCount = allDebates
                .SelectMany(d => d.Turns.Where(t => t.AgentId == agent.Id))
                .Sum(t => t.Reactions.Count(r => r.Type == "insightful"));

            var totalDebateCount = allDebates.Count;
            var winRate = totalDebateCount > 0 ? (double)wins / totalDebateCount * 100 : 0;

            // Underrated score: high win rate + low reputation
            var underratedScore = totalDebateCount >= 2
                ? winRate - agent.ReputationScore
                : -999;

            // Top tag
            var topTag = allDebates
                .SelectMany(d => d.DebateTags)
                .GroupBy(dt => dt.Tag.DisplayName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return new
            {
                agent.Id,
                agent.Name,
                agent.Description,
                agent.AvatarUrl,
                agent.Persona,
                agent.ReputationScore,
                Stats = new
                {
                    Wins = wins,
                    Losses = losses,
                    Draws = draws,
                    TotalDebates = totalDebateCount,
                    WinRate = Math.Round(winRate, 1),
                    PeriodWins = periodWins,
                    PeriodLosses = periodLosses,
                    ControversialDebates = controversialCount,
                    AvgVoteMargin = debatesWithVotes > 0 ? Math.Round(totalVoteMargin / debatesWithVotes, 1) : 0,
                    TotalReactions = totalReactions,
                    DisagreeReactions = disagreeCount,
                    InsightfulReactions = insightfulCount,
                    TopTag = topTag,
                    UnderratedScore = Math.Round(underratedScore, 1),
                },
            };
        }).ToList();

        // Sort
        var sorted = sort switch
        {
            "controversial" => entries
                .OrderByDescending(e => e.Stats.ControversialDebates)
                .ThenByDescending(e => e.Stats.DisagreeReactions)
                .ToList(),
            "underrated" => entries
                .OrderByDescending(e => e.Stats.UnderratedScore)
                .ToList(),
            "winrate" => entries
                .OrderByDescending(e => e.Stats.WinRate)
                .ThenByDescending(e => e.Stats.TotalDebates)
                .ToList(),
            "reactions" => entries
                .OrderByDescending(e => e.Stats.TotalReactions)
                .ToList(),
            _ => entries // "top" - most wins in period
                .OrderByDescending(e => e.Stats.PeriodWins)
                .ThenByDescending(e => e.Stats.WinRate)
                .ToList(),
        };

        return Ok(new { sort, period, agents = sorted });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();
        return Ok(agent);
    }

    [HttpGet("{id:guid}/rivals")]
    public async Task<IActionResult> GetRivals(Guid id)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();

        var debates = await _db.Debates
            .Where(d => d.Status == DebateStatus.Completed && (d.ProponentId == id || d.OpponentId == id))
            .Include(d => d.Votes)
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .ToListAsync();

        var rivals = debates
            .GroupBy(d => d.ProponentId == id ? d.OpponentId : d.ProponentId)
            .Select(g =>
            {
                var rivalId = g.Key;
                var rivalAgent = g.First().ProponentId == rivalId ? g.First().Proponent : g.First().Opponent;
                var wins = 0;
                var losses = 0;

                foreach (var d in g)
                {
                    var proVotes = d.Votes.Count(v => v.VotedForAgentId == d.ProponentId);
                    var oppVotes = d.Votes.Count(v => v.VotedForAgentId == d.OpponentId);
                    var isProponent = d.ProponentId == id;
                    if ((isProponent ? proVotes : oppVotes) > (isProponent ? oppVotes : proVotes)) wins++;
                    else if ((isProponent ? proVotes : oppVotes) < (isProponent ? oppVotes : proVotes)) losses++;
                }

                return new
                {
                    RivalId = rivalId,
                    RivalName = rivalAgent.Name,
                    Matchups = g.Count(),
                    Wins = wins,
                    Losses = losses,
                    Draws = g.Count() - wins - losses,
                };
            })
            .OrderByDescending(r => r.Matchups)
            .ToList();

        return Ok(rivals);
    }
}
