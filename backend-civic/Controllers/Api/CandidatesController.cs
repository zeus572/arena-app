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
[Route("api/candidates")]
public class CandidatesController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;
    private readonly ICurrentUserService _user;

    public CandidatesController(CivicDbContext db, ICivicCatalog catalog, ICurrentUserService user)
    {
        _db = db;
        _catalog = catalog;
        _user = user;
    }

    // GET /api/candidates?office=President&party=...&state=CA&district=3&take=50
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CandidateSummaryDto>>> List(
        [FromQuery] string? office,
        [FromQuery] string? party,
        [FromQuery] string? state,
        [FromQuery] int? district,
        [FromQuery] int take = 100)
    {
        if (take is < 1 or > 200) take = 100;

        if (!TryParseOffice(office, out var parsedOffice, out var error))
        {
            return ValidationProblem(error);
        }

        var query = _db.VirtualCandidates.AsQueryable();
        if (parsedOffice is not null) query = query.Where(c => c.Office == parsedOffice);
        if (!string.IsNullOrWhiteSpace(party)) query = query.Where(c => c.Party == party);
        if (!string.IsNullOrWhiteSpace(state)) query = query.Where(c => c.State == state);
        if (district is not null) query = query.Where(c => c.District == district);

        var items = await query
            .OrderBy(c => c.Office)
            .ThenBy(c => c.Name)
            .Take(take)
            .ToListAsync();

        return Ok(items.Select(c => c.ToSummaryDto()));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<CandidateDetailDto>> GetBySlug(string slug)
    {
        var c = await LoadFull(slug);
        if (c is null) return NotFound();
        var postCount = await _db.CampaignPosts.CountAsync(p => p.CandidateId == c.Id);
        return Ok(c.ToDetailDto(_catalog, postCount));
    }

    [HttpGet("{slug}/platform")]
    public async Task<ActionResult<IEnumerable<PlatformPlankDto>>> Platform(string slug)
    {
        var c = await _db.VirtualCandidates
            .Include(x => x.PlatformPlanks)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        return c is null ? NotFound() : Ok(c.PlatformPlanks.Select(p => p.ToDto()));
    }

    [HttpGet("{slug}/sources")]
    public async Task<ActionResult<IEnumerable<CandidateSourceDto>>> Sources(string slug)
    {
        var c = await _db.VirtualCandidates
            .Include(x => x.Sources)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        return c is null
            ? NotFound()
            : Ok(c.Sources.OrderBy(s => s.Priority).Select(s => s.ToDto()));
    }

    [HttpGet("{slug}/values")]
    public async Task<ActionResult<IEnumerable<CandidateValueDto>>> Values(string slug)
    {
        var c = await _db.VirtualCandidates
            .Include(x => x.AxisScores)
            .FirstOrDefaultAsync(x => x.Slug == slug);
        return c is null ? NotFound() : Ok(c.AxisScores.ToValueDtos(_catalog));
    }

    // GET /api/candidates/:slug/posts?cursor=<opaque>&limit=20
    [HttpGet("{slug}/posts")]
    public async Task<ActionResult<CampaignFeedDto>> Posts(
        string slug,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;

        var candidate = await _db.VirtualCandidates.FirstOrDefaultAsync(c => c.Slug == slug);
        if (candidate is null) return NotFound();

        // Feed tailoring: public/system posts plus the caller's own campaign responses.
        var userId = _user.GetCurrentUserId();
        var query = _db.CampaignPosts
            .Include(p => p.Fragments)
            .Where(p => p.CandidateId == candidate.Id)
            .Where(p => p.OwnerUserId == null || p.OwnerUserId == userId);

        if (FeedCursor.TryDecode(cursor, out var before))
        {
            query = query.Where(p => p.CreatedAt < before);
        }

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = posts.Count > limit;
        if (hasMore) posts = posts.Take(limit).ToList();

        var briefings = await ResolveBriefingPreviewsAsync(posts);

        return Ok(new CampaignFeedDto
        {
            Items = posts.Select(p =>
            {
                var b = LookupBriefing(briefings, p.TriggerBriefingSlug);
                return p.ToDto(candidate, b?.Headline, b?.Summary);
            }).ToList(),
            NextCursor = hasMore ? FeedCursor.Encode(posts[^1].CreatedAt) : null,
        });
    }

    private async Task<Dictionary<string, (string Headline, string Summary)>> ResolveBriefingPreviewsAsync(
        IEnumerable<CampaignPost> posts)
    {
        var slugs = posts.Where(p => p.TriggerBriefingSlug != null)
            .Select(p => p.TriggerBriefingSlug!).Distinct().ToList();
        if (slugs.Count == 0) return new();
        return await _db.Briefings.Where(b => slugs.Contains(b.Slug))
            .ToDictionaryAsync(b => b.Slug, b => new ValueTuple<string, string>(b.Headline, b.Summary30));
    }

    private static (string Headline, string Summary)? LookupBriefing(
        Dictionary<string, (string Headline, string Summary)> map, string? slug)
        => slug != null && map.TryGetValue(slug, out var b) ? b : null;

    [HttpPost("{slug}/follow")]
    public Task<ActionResult> Follow(string slug) => SetEngagement(slug, follow: true, on: true);

    [HttpDelete("{slug}/follow")]
    public Task<ActionResult> Unfollow(string slug) => SetEngagement(slug, follow: true, on: false);

    [HttpPost("{slug}/mute")]
    public Task<ActionResult> Mute(string slug) => SetEngagement(slug, follow: false, on: true);

    [HttpDelete("{slug}/mute")]
    public Task<ActionResult> Unmute(string slug) => SetEngagement(slug, follow: false, on: false);

    private async Task<ActionResult> SetEngagement(string slug, bool follow, bool on)
    {
        var candidate = await _db.VirtualCandidates.FirstOrDefaultAsync(c => c.Slug == slug);
        if (candidate is null) return NotFound();

        var userId = _user.GetCurrentUserId();

        if (follow)
        {
            var existing = await _db.CandidateFollows
                .FirstOrDefaultAsync(f => f.UserId == userId && f.CandidateId == candidate.Id);
            if (on && existing is null)
            {
                _db.CandidateFollows.Add(new CandidateFollow { Id = Guid.NewGuid(), UserId = userId, CandidateId = candidate.Id });
            }
            else if (!on && existing is not null)
            {
                _db.CandidateFollows.Remove(existing);
            }
        }
        else
        {
            var existing = await _db.CandidateMutes
                .FirstOrDefaultAsync(m => m.UserId == userId && m.CandidateId == candidate.Id);
            if (on && existing is null)
            {
                _db.CandidateMutes.Add(new CandidateMute { Id = Guid.NewGuid(), UserId = userId, CandidateId = candidate.Id });
            }
            else if (!on && existing is not null)
            {
                _db.CandidateMutes.Remove(existing);
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Task<VirtualCandidate?> LoadFull(string slug) =>
        _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.AxisScores)
            .Include(c => c.IssueTones)
            .FirstOrDefaultAsync(c => c.Slug == slug);

    private static bool TryParseOffice(string? raw, out CandidateOffice? parsed, out string error)
    {
        parsed = null;
        error = "";
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (Enum.TryParse<CandidateOffice>(raw, ignoreCase: true, out var p))
        {
            parsed = p;
            return true;
        }
        error = $"Unknown office '{raw}'.";
        return false;
    }
}
