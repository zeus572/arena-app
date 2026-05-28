using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

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
            .ThenBy(b => b.CreatedAt)
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
}
