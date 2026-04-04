using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class DebatesController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly ICurrentUserService _userService;
    private readonly TaggingService _tagging;

    public DebatesController(ArenaDbContext db, ICurrentUserService userService, TaggingService tagging)
    {
        _db = db;
        _userService = userService;
        _tagging = tagging;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var debates = await _db.Debates
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(debates);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var debate = await _db.Debates
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .Include(d => d.GeneratedTopic)
            .Include(d => d.Turns.OrderBy(t => t.TurnNumber))
                .ThenInclude(t => t.Agent)
            .Include(d => d.Turns)
                .ThenInclude(t => t.Reactions)
            .Include(d => d.Reactions)
            .Include(d => d.Votes)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (debate is null) return NotFound();

        var proponentVotes = debate.Votes.Count(v => v.VotedForAgentId == debate.ProponentId);
        var opponentVotes = debate.Votes.Count(v => v.VotedForAgentId == debate.OpponentId);

        var formatConfig = DebateFormatConfig.Get(debate.Format);

        return Ok(new
        {
            debate.Id,
            debate.Topic,
            debate.Description,
            Status = debate.Status.ToString(),
            debate.Format,
            FormatConfig = new
            {
                formatConfig.DisplayName,
                formatConfig.MaxTurns,
                formatConfig.MaxCharactersPerTurn,
                formatConfig.HasCompromisePhase,
                formatConfig.HasWildcards,
                formatConfig.HasCommentary,
            },
            Proponent = new { debate.Proponent.Id, debate.Proponent.Name, debate.Proponent.AvatarUrl, debate.Proponent.Persona, debate.Proponent.AgentType, debate.Proponent.Era },
            Opponent = new { debate.Opponent.Id, debate.Opponent.Name, debate.Opponent.AvatarUrl, debate.Opponent.Persona, debate.Opponent.AgentType, debate.Opponent.Era },
            debate.CreatedAt,
            debate.Source,
            NewsInfo = debate.Source == "breaking" && debate.GeneratedTopic != null
                ? new
                {
                    Headline = debate.GeneratedTopic.NewsHeadline,
                    Source = debate.GeneratedTopic.NewsSource,
                    PublishedAt = debate.GeneratedTopic.NewsPublishedAt,
                }
                : null,
            ProponentVotes = proponentVotes,
            OpponentVotes = opponentVotes,
            Reactions = debate.Reactions
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            Turns = debate.Turns.OrderBy(t => t.TurnNumber).Select(t => new
            {
                t.Id,
                t.DebateId,
                t.AgentId,
                Agent = new { t.Agent.Id, t.Agent.Name, t.Agent.AvatarUrl },
                t.TurnNumber,
                Type = t.Type.ToString(),
                t.Content,
                t.CitationsJson,
                t.AnalysisJson,
                t.CreatedAt,
                Reactions = t.Reactions
                    .GroupBy(r => r.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
            }),
        });
    }

    [HttpPost]
    [Authorize(Policy = "Premium")]
    public async Task<IActionResult> Create([FromBody] CreateDebateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
            return BadRequest(new { error = "Topic is required." });

        var user = await _userService.GetOrCreateUserAsync();

        Guid proponentId, opponentId;

        if (request.ProponentId.HasValue && request.OpponentId.HasValue)
        {
            proponentId = request.ProponentId.Value;
            opponentId = request.OpponentId.Value;
        }
        else
        {
            var agents = await _db.Agents.ToListAsync();
            if (agents.Count < 2)
                return BadRequest(new { error = "Not enough agents to create a debate." });

            var shuffled = agents.OrderBy(_ => Guid.NewGuid()).Take(2).ToList();
            proponentId = request.ProponentId ?? shuffled[0].Id;
            opponentId = request.OpponentId ?? shuffled[1].Id;
        }

        if (proponentId == opponentId)
            return BadRequest(new { error = "Proponent and opponent must be different agents." });

        var format = request.Format ?? "standard";
        if (!DebateFormatConfig.All.ContainsKey(format))
            return BadRequest(new { error = $"Invalid format: {format}" });

        var debate = new Debate
        {
            Id = Guid.NewGuid(),
            Topic = request.Topic.Trim(),
            Description = request.Description?.Trim(),
            Format = format,
            Status = DebateStatus.Pending,
            ProponentId = proponentId,
            OpponentId = opponentId,
            StartedByUserId = user.Id,
        };

        _db.Debates.Add(debate);
        await _db.SaveChangesAsync();

        await _tagging.ExtractAndAssignTagsAsync(_db, debate);

        return CreatedAtAction(nameof(GetById), new { id = debate.Id }, new { debate.Id, debate.Topic, debate.Status });
    }

    [HttpPost("{id:guid}/votes")]
    public async Task<IActionResult> CastVote(Guid id, [FromBody] CastVoteRequest request)
    {
        var debate = await _db.Debates.FindAsync(id);
        if (debate is null) return NotFound();

        if (request.VotedForAgentId != debate.ProponentId && request.VotedForAgentId != debate.OpponentId)
            return BadRequest(new { error = "Must vote for the proponent or opponent." });

        var user = await _userService.GetOrCreateUserAsync();

        var alreadyVoted = await _db.Votes.AnyAsync(v => v.DebateId == id && v.UserId == user.Id);
        if (alreadyVoted)
            return Conflict(new { error = "You have already voted on this debate." });

        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            DebateId = id,
            UserId = user.Id,
            VotedForAgentId = request.VotedForAgentId,
        };

        _db.Votes.Add(vote);
        await _db.SaveChangesAsync();

        return Created("", new { vote.Id, vote.DebateId, vote.VotedForAgentId });
    }
}
