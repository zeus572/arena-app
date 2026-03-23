using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
public class ReactionsController : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "like", "fire", "think", "disagree", "insightful"
    };

    private readonly ArenaDbContext _db;
    private readonly ICurrentUserService _userService;

    public ReactionsController(ArenaDbContext db, ICurrentUserService userService)
    {
        _db = db;
        _userService = userService;
    }

    [HttpPost("api/debates/{debateId:guid}/reactions")]
    public async Task<IActionResult> ReactToDebate(Guid debateId, [FromBody] CreateReactionRequest request)
    {
        if (!AllowedTypes.Contains(request.Type))
            return BadRequest(new { error = $"Invalid reaction type. Allowed: {string.Join(", ", AllowedTypes)}" });

        var debate = await _db.Debates.FindAsync(debateId);
        if (debate is null) return NotFound();

        var user = await _userService.GetOrCreateUserAsync();
        var type = request.Type.ToLowerInvariant();

        var exists = await _db.Reactions.AnyAsync(r =>
            r.UserId == user.Id && r.DebateId == debateId && r.TurnId == null && r.Type == type);
        if (exists)
            return Conflict(new { error = "You already reacted with this type." });

        var reaction = new Reaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DebateId = debateId,
            Type = type,
        };

        _db.Reactions.Add(reaction);
        await _db.SaveChangesAsync();

        return Created("", new { reaction.Id, reaction.Type });
    }

    [HttpPost("api/turns/{turnId:guid}/reactions")]
    public async Task<IActionResult> ReactToTurn(Guid turnId, [FromBody] CreateReactionRequest request)
    {
        if (!AllowedTypes.Contains(request.Type))
            return BadRequest(new { error = $"Invalid reaction type. Allowed: {string.Join(", ", AllowedTypes)}" });

        var turn = await _db.Turns.FindAsync(turnId);
        if (turn is null) return NotFound();

        var user = await _userService.GetOrCreateUserAsync();
        var type = request.Type.ToLowerInvariant();

        var exists = await _db.Reactions.AnyAsync(r =>
            r.UserId == user.Id && r.TurnId == turnId && r.Type == type);
        if (exists)
            return Conflict(new { error = "You already reacted with this type." });

        var reaction = new Reaction
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DebateId = turn.DebateId,
            TurnId = turnId,
            Type = type,
        };

        _db.Reactions.Add(reaction);
        await _db.SaveChangesAsync();

        return Created("", new { reaction.Id, reaction.Type });
    }
}
