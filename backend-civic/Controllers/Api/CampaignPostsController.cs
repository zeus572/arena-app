using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Campaign;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/posts")]
public class CampaignPostsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ICampaignReactionService _reactions;

    public CampaignPostsController(CivicDbContext db, ICurrentUserService user, ICampaignReactionService reactions)
    {
        _db = db;
        _user = user;
        _reactions = reactions;
    }

    // GET /api/posts/:id — strongly-consistent post detail incl. fragments + aggregates.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignPostDto>> Get(Guid id)
    {
        var post = await _db.CampaignPosts
            .Include(p => p.Fragments)
            .Include(p => p.Candidate)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        string? headline = null;
        if (post.TriggerBriefingSlug is not null)
        {
            headline = await _db.Briefings
                .Where(b => b.Slug == post.TriggerBriefingSlug)
                .Select(b => b.Headline)
                .FirstOrDefaultAsync();
        }

        return Ok(post.ToDto(post.Candidate, headline));
    }

    // GET /api/posts/:id/heatmap — fragment-level reaction aggregates.
    [HttpGet("{id:guid}/heatmap")]
    public async Task<ActionResult<PostHeatmapDto>> Heatmap(Guid id)
    {
        var post = await _db.CampaignPosts
            .Include(p => p.Fragments)
            .FirstOrDefaultAsync(p => p.Id == id);
        return post is null ? NotFound() : Ok(post.ToHeatmapDto());
    }

    // POST /api/posts/:id/reactions { type: "up" | "down" }
    [HttpPost("{id:guid}/reactions")]
    public Task<ActionResult<ReactionResultDto>> React(Guid id, [FromBody] ReactionRequestDto body) =>
        ReactInternal(id, null, body);

    // DELETE /api/posts/:id/reactions
    [HttpDelete("{id:guid}/reactions")]
    public Task<ActionResult<ReactionResultDto>> Unreact(Guid id) =>
        RemoveInternal(id, null);

    // POST /api/posts/:id/fragments/:fragmentId/reactions { type }
    [HttpPost("{id:guid}/fragments/{fragmentId:guid}/reactions")]
    public Task<ActionResult<ReactionResultDto>> ReactFragment(Guid id, Guid fragmentId, [FromBody] ReactionRequestDto body) =>
        ReactInternal(id, fragmentId, body);

    // DELETE /api/posts/:id/fragments/:fragmentId/reactions
    [HttpDelete("{id:guid}/fragments/{fragmentId:guid}/reactions")]
    public Task<ActionResult<ReactionResultDto>> UnreactFragment(Guid id, Guid fragmentId) =>
        RemoveInternal(id, fragmentId);

    private async Task<ActionResult<ReactionResultDto>> ReactInternal(Guid postId, Guid? fragmentId, ReactionRequestDto body)
    {
        if (!TryParseType(body?.Type, out var type))
        {
            return ValidationProblem("Reaction type must be 'up' or 'down'.");
        }

        var userId = _user.GetCurrentUserId();
        var result = await _reactions.ReactAsync(userId, postId, fragmentId, type);
        return ToActionResult(postId, fragmentId, result);
    }

    private async Task<ActionResult<ReactionResultDto>> RemoveInternal(Guid postId, Guid? fragmentId)
    {
        var userId = _user.GetCurrentUserId();
        var result = await _reactions.RemoveAsync(userId, postId, fragmentId);
        return ToActionResult(postId, fragmentId, result);
    }

    private ActionResult<ReactionResultDto> ToActionResult(Guid postId, Guid? fragmentId, ReactionResult result)
    {
        return result.Outcome switch
        {
            ReactionOutcome.PostNotFound => NotFound(),
            ReactionOutcome.FragmentNotFound => NotFound(),
            _ => Ok(new ReactionResultDto
            {
                PostId = postId,
                FragmentId = fragmentId,
                PostUp = result.Post.Up,
                PostDown = result.Post.Down,
                FragmentUp = result.Fragment?.Up,
                FragmentDown = result.Fragment?.Down,
            }),
        };
    }

    private static bool TryParseType(string? raw, out ReactionType type)
    {
        type = ReactionType.Up;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return Enum.TryParse(raw, ignoreCase: true, out type);
    }
}
