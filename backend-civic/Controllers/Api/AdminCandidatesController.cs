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
[Route("api/admin")]
public class AdminCandidatesController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly CampaignPostGenerationService _generator;

    public AdminCandidatesController(CivicDbContext db, CampaignPostGenerationService generator)
    {
        _db = db;
        _generator = generator;
    }

    // POST /api/admin/candidates/:slug/posts/generate { triggerBriefingId?, force? }
    [HttpPost("candidates/{slug}/posts/generate")]
    public async Task<ActionResult<CampaignPostDto>> Generate(string slug, [FromBody] GeneratePostRequestDto? body)
    {
        var candidate = await _db.VirtualCandidates.FirstOrDefaultAsync(c => c.Slug == slug);
        if (candidate is null) return NotFound();

        Briefing? briefing = null;
        var trigger = PostTrigger.Platform;
        if (body?.TriggerBriefingId is Guid briefingId)
        {
            briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Id == briefingId);
            if (briefing is null) return ValidationProblem($"Briefing {briefingId} not found.");
            trigger = PostTrigger.Briefing;
        }

        var post = await _generator.GenerateForCandidateAsync(candidate.Id, briefing, trigger, body?.Force ?? false);
        if (post is null)
        {
            return Conflict(new { error = "Generation skipped — candidate is on cooldown or over daily budget. Pass force=true to override." });
        }

        return Ok(post.ToDto(candidate, briefing?.Headline));
    }

    // GET /api/admin/budget — per-candidate post counts (proxy for LLM spend).
    [HttpGet("budget")]
    public async Task<ActionResult<AdminBudgetDto>> Budget()
    {
        var dayCutoff = DateTime.UtcNow.AddHours(-24);

        var candidates = await _db.VirtualCandidates
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Slug, c.Name })
            .ToListAsync();

        var stats = await _db.CampaignPosts
            .GroupBy(p => p.CandidateId)
            .Select(g => new
            {
                CandidateId = g.Key,
                Total = g.Count(),
                Last24h = g.Count(p => p.CreatedAt >= dayCutoff),
                Intensity5Last24h = g.Count(p => p.Intensity == 5 && p.CreatedAt >= dayCutoff),
                LastPostAt = (DateTime?)g.Max(p => p.CreatedAt),
            })
            .ToListAsync();

        var byId = stats.ToDictionary(s => s.CandidateId);

        var rows = candidates.Select(c =>
        {
            byId.TryGetValue(c.Id, out var s);
            return new CandidateBudgetDto
            {
                CandidateId = c.Id,
                Slug = c.Slug,
                Name = c.Name,
                PostsTotal = s?.Total ?? 0,
                PostsLast24h = s?.Last24h ?? 0,
                Intensity5Last24h = s?.Intensity5Last24h ?? 0,
                LastPostAt = s?.LastPostAt is { } d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null,
            };
        }).ToList();

        return Ok(new AdminBudgetDto
        {
            TotalPosts = rows.Sum(r => r.PostsTotal),
            PostsLast24h = rows.Sum(r => r.PostsLast24h),
            Candidates = rows,
        });
    }
}
