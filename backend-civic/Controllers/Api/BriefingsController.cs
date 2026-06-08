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
    public async Task<ActionResult<BriefingPageDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Briefings
            .OrderBy(b => b.IssueOrder)
            .ThenByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new BriefingPageDto
        {
            Items = items.Select(b => b.ToSummaryDto()).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<BriefingDto>> GetBySlug(string slug)
    {
        var b = await _db.Briefings
            .Include(x => x.WordsToKnow)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        if (b is null) return NotFound();

        // Resolve the original article so the briefing can credit + link its source.
        var source = b.SourceNewsItemId is Guid newsId
            ? await _db.NewsItems.FirstOrDefaultAsync(n => n.Id == newsId)
            : null;
        return Ok(b.ToDto(source));
    }

    // GET /api/briefings/{slug}/candidate-reactions — Virtual Candidate posts
    // triggered by this briefing, most-reacted first.
    [HttpGet("{slug}/candidate-reactions")]
    public async Task<ActionResult<IEnumerable<CampaignPostDto>>> CandidateReactions(
        string slug, [FromQuery] int take = 10)
    {
        if (take is < 1 or > 50) take = 10;

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == slug);
        if (briefing is null) return NotFound();

        var posts = await _db.CampaignPosts
            .Include(p => p.Fragments)
            .Include(p => p.Candidate)
            .Where(p => p.TriggerBriefingSlug == slug)
            .OrderByDescending(p => p.UpCount + p.DownCount)
            .ThenByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync();

        return Ok(posts.Select(p => p.ToDto(p.Candidate, briefing.Headline, briefing.Summary30)));
    }
}
