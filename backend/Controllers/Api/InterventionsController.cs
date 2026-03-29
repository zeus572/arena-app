using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/debates/{debateId:guid}/interventions")]
public class InterventionsController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly ICurrentUserService _userService;

    public InterventionsController(ArenaDbContext db, ICurrentUserService userService)
    {
        _db = db;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInterventions(Guid debateId)
    {
        var interventions = await _db.Interventions
            .Where(i => i.DebateId == debateId)
            .OrderByDescending(i => i.Upvotes)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Content,
                i.Upvotes,
                i.Used,
                i.UsedInTurnNumber,
                i.CreatedAt,
                AuthorName = i.User.DisplayName ?? "Anonymous",
            })
            .ToListAsync();

        return Ok(interventions);
    }

    [HttpPost]
    [Authorize(Policy = "Premium")]
    public async Task<IActionResult> SubmitIntervention(Guid debateId, [FromBody] SubmitInterventionRequest request)
    {
        var debate = await _db.Debates.FindAsync(debateId);
        if (debate is null) return NotFound();

        if (debate.Status != DebateStatus.Active && debate.Status != DebateStatus.Compromising)
            return BadRequest(new { error = "Can only submit questions to active debates." });

        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length < 10)
            return BadRequest(new { error = "Question must be at least 10 characters." });

        if (request.Content.Length > 280)
            return BadRequest(new { error = "Question must be 280 characters or less." });

        var user = await _userService.GetOrCreateUserAsync();

        // Max 3 interventions per user per debate
        var count = await _db.Interventions.CountAsync(i => i.DebateId == debateId && i.UserId == user.Id);
        if (count >= 3)
            return BadRequest(new { error = "Maximum 3 questions per debate." });

        var intervention = new Intervention
        {
            Id = Guid.NewGuid(),
            DebateId = debateId,
            UserId = user.Id,
            Content = request.Content.Trim(),
        };

        _db.Interventions.Add(intervention);
        await _db.SaveChangesAsync();

        return Created("", new { intervention.Id, intervention.Content });
    }

    [HttpPost("{interventionId:guid}/upvote")]
    [Authorize]
    public async Task<IActionResult> UpvoteIntervention(Guid debateId, Guid interventionId)
    {
        var intervention = await _db.Interventions.FirstOrDefaultAsync(
            i => i.Id == interventionId && i.DebateId == debateId);
        if (intervention is null) return NotFound();

        intervention.Upvotes++;
        await _db.SaveChangesAsync();

        return Ok(new { intervention.Upvotes });
    }
}

public record SubmitInterventionRequest(string Content);
