using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Campaign;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/campaign")]
public class CampaignFeedController : ControllerBase
{
    private readonly CivicDbContext _db;

    public CampaignFeedController(CivicDbContext db) => _db = db;

    // GET /api/campaign/feed?office=&party=&state=&district=&tone=&minIntensity=&issue=&sort=recent|top|controversial|trending&cursor=&limit=
    [HttpGet("feed")]
    public async Task<ActionResult<CampaignFeedDto>> Feed(
        [FromQuery] string? office,
        [FromQuery] string? party,
        [FromQuery] string? state,
        [FromQuery] int? district,
        [FromQuery] string? tone,
        [FromQuery] int? minIntensity,
        [FromQuery] string? issue,
        [FromQuery] string sort = "recent",
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;

        var query = _db.CampaignPosts
            .Include(p => p.Fragments)
            .Include(p => p.Candidate)
            .AsQueryable();

        if (TryParseEnum<CandidateOffice>(office, out var off))
            query = query.Where(p => p.Candidate!.Office == off);
        if (!string.IsNullOrWhiteSpace(party))
            query = query.Where(p => p.Candidate!.Party == party);
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(p => p.Candidate!.State == state);
        if (district is not null)
            query = query.Where(p => p.Candidate!.District == district);
        if (TryParseEnum<CampaignTone>(tone, out var t))
            query = query.Where(p => p.Tone == t);
        if (minIntensity is >= 1 and <= 5)
            query = query.Where(p => p.Intensity >= minIntensity);
        if (!string.IsNullOrWhiteSpace(issue))
            query = query.Where(p => p.IssueTags.Contains(issue));

        var isRecent = string.Equals(sort, "recent", StringComparison.OrdinalIgnoreCase);

        // Cursor only composes with reverse-chronological order.
        if (isRecent && FeedCursor.TryDecode(cursor, out var before))
        {
            query = query.Where(p => p.CreatedAt < before);
        }

        query = sort?.ToLowerInvariant() switch
        {
            "top" => query.OrderByDescending(p => p.UpCount).ThenByDescending(p => p.CreatedAt),
            "controversial" => query
                .OrderByDescending(p => p.UpCount < p.DownCount ? p.UpCount : p.DownCount)
                .ThenByDescending(p => p.CreatedAt),
            "trending" => query
                .OrderByDescending(p => p.UpCount + p.DownCount)
                .ThenByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt),
        };

        var posts = await query.Take(limit + 1).ToListAsync();
        var hasMore = posts.Count > limit;
        if (hasMore) posts = posts.Take(limit).ToList();

        // Resolve briefing headlines for posts that quote one.
        var slugs = posts.Where(p => p.TriggerBriefingSlug != null)
            .Select(p => p.TriggerBriefingSlug!).Distinct().ToList();
        var headlines = slugs.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Briefings.Where(b => slugs.Contains(b.Slug))
                .ToDictionaryAsync(b => b.Slug, b => b.Headline);

        return Ok(new CampaignFeedDto
        {
            Items = posts.Select(p => p.ToDto(
                p.Candidate,
                p.TriggerBriefingSlug != null && headlines.TryGetValue(p.TriggerBriefingSlug, out var h) ? h : null))
                .ToList(),
            NextCursor = isRecent && hasMore ? FeedCursor.Encode(posts[^1].CreatedAt) : null,
        });
    }

    private static bool TryParseEnum<TEnum>(string? raw, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        return !string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, ignoreCase: true, out value);
    }
}
