using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

// NOTE: Virtual Candidate reactions to a briefing are exposed below via
// GET /api/briefings/{slug}/candidate-reactions.

[ApiController]
[AllowAnonymous]
[Route("api/briefings")]
public class BriefingsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;

    public BriefingsController(CivicDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>The caller's chosen locality (state code), or null for national-only.</summary>
    private async Task<string?> CurrentLocalityAsync()
    {
        var userId = _user.GetCurrentUserId();
        return await _db.UserProfiles
            .Where(u => u.UserId == userId)
            .Select(u => u.LocalityState)
            .FirstOrDefaultAsync();
    }

    [HttpGet]
    public async Task<ActionResult<BriefingPageDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        // Hard wall + blend: national briefings (Locality null) plus the caller's
        // own local briefings, ordered together by issue/recency.
        var locality = await CurrentLocalityAsync();
        var query = _db.Briefings
            .Where(b => b.Locality == null || b.Locality == locality)
            .OrderBy(b => b.IssueOrder)
            .ThenByDescending(b => b.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve the upstream publisher for this page's news-sourced briefings in one
        // query so each feed card can show a small per-source moniker (NPR / BBC / local).
        var sourceIds = items
            .Where(b => b.SourceNewsItemId is not null)
            .Select(b => b.SourceNewsItemId!.Value)
            .Distinct()
            .ToList();
        var publishers = sourceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.NewsItems
                .Where(n => sourceIds.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, n => n.Source);

        var dtos = items.Select(b =>
        {
            var dto = b.ToSummaryDto();
            if (b.SourceNewsItemId is Guid id && publishers.TryGetValue(id, out var publisher))
                dto.SourcePublisher = publisher;
            return dto;
        }).ToList();

        return Ok(new BriefingPageDto
        {
            Items = dtos,
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

        // Hard wall: a local briefing is hidden from out-of-locality readers.
        if (b.Locality is not null && b.Locality != await CurrentLocalityAsync())
            return NotFound();

        // Resolve the original article so the briefing can credit + link its source.
        var source = b.SourceNewsItemId is Guid newsId
            ? await _db.NewsItems.FirstOrDefaultAsync(n => n.Id == newsId)
            : null;

        // Resolve the coalition born from this briefing (most recent, excluding dead ones)
        // so the article can call out a live bill for readers to join.
        var coalition = await _db.Provisions
            .Where(p => p.SourceBriefingId == b.Id && p.State != ProvisionState.Died)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        return Ok(b.ToDto(source, coalition));
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
