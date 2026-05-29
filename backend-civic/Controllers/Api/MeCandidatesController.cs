using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Campaign;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/me")]
public class MeCandidatesController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ICandidateMatchService _matches;

    public MeCandidatesController(CivicDbContext db, ICurrentUserService user, ICandidateMatchService matches)
    {
        _db = db;
        _user = user;
        _matches = matches;
    }

    // GET /api/me/candidate-matches — requires a Values Profile (HasProfile=false otherwise).
    [HttpGet("candidate-matches")]
    public async Task<ActionResult<CandidateMatchesDto>> Matches()
    {
        var userId = _user.GetCurrentUserId();
        var result = await _matches.GetMatchesAsync(userId);
        return Ok(new CandidateMatchesDto
        {
            HasProfile = result.HasProfile,
            TopMatches = result.TopMatches.Select(ToItem).ToList(),
            ProductiveChallenges = result.ProductiveChallenges.Select(ToItem).ToList(),
            SurprisingAgreements = result.SurprisingAgreements.Select(ToItem).ToList(),
        });
    }

    // GET /api/me/campaign-feed — reverse-chron feed with the user's muted candidates removed.
    [HttpGet("campaign-feed")]
    public async Task<ActionResult<CampaignFeedDto>> Feed(
        [FromQuery] string? cursor, [FromQuery] int limit = 20)
    {
        if (limit is < 1 or > 100) limit = 20;
        var userId = _user.GetCurrentUserId();

        var muted = await _db.CandidateMutes
            .Where(m => m.UserId == userId)
            .Select(m => m.CandidateId)
            .ToListAsync();

        var query = _db.CampaignPosts
            .Include(p => p.Fragments)
            .Include(p => p.Candidate)
            .Where(p => !muted.Contains(p.CandidateId));

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

        return Ok(new CampaignFeedDto
        {
            Items = posts.Select(p => p.ToDto(p.Candidate)).ToList(),
            NextCursor = hasMore ? FeedCursor.Encode(posts[^1].CreatedAt) : null,
        });
    }

    private static CandidateMatchItemDto ToItem(CandidateMatchItem m) => new()
    {
        Candidate = m.Candidate.ToSummaryDto(),
        Score = Math.Round(m.Score, 3),
        Reason = m.Reason,
    };
}
