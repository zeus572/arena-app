using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

// NOTE: Virtual Candidate reactions to a briefing are exposed below via
// GET /api/briefings/{slug}/candidate-reactions.

[ApiController]
[AllowAnonymous]
[Route("api/briefings")]
public class BriefingsController : ControllerBase
{
    private readonly CivicDbContext _db;

    public BriefingsController(CivicDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BriefingSummaryDto>>> List()
    {
        var items = await _db.Briefings
            .OrderBy(b => b.IssueOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();
        return Ok(items.Select(b => b.ToSummaryDto()));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<BriefingDto>> GetBySlug(string slug)
    {
        var b = await _db.Briefings
            .Include(x => x.WordsToKnow)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        return b is null ? NotFound() : Ok(b.ToDto());
    }

    // GET /api/briefings/{slug}/candidate-reactions — Virtual Candidate posts
    // triggered by this briefing, most-reacted first.
    [HttpGet("{slug}/candidate-reactions")]
    public async Task<ActionResult<IEnumerable<CampaignPostDto>>> CandidateReactions(
        string slug, [FromQuery] int take = 10)
    {
        if (take is < 1 or > 50) take = 10;

        var exists = await _db.Briefings.AnyAsync(b => b.Slug == slug);
        if (!exists) return NotFound();

        var posts = await _db.CampaignPosts
            .Include(p => p.Fragments)
            .Include(p => p.Candidate)
            .Where(p => p.TriggerBriefingSlug == slug)
            .OrderByDescending(p => p.UpCount + p.DownCount)
            .ThenByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync();

        return Ok(posts.Select(p => p.ToDto(p.Candidate)));
    }
}
