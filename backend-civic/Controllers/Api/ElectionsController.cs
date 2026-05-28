using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/elections")]
public class ElectionsController : ControllerBase
{
    private readonly CivicDbContext _db;

    public ElectionsController(CivicDbContext db) => _db = db;

    // GET /api/elections?scope=national&region=CA&take=10
    // Returns upcoming elections (scheduledAt >= now), ordered soonest-first.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ElectionDto>>> List(
        [FromQuery] string? scope,
        [FromQuery] string? region,
        [FromQuery] int take = 25)
    {
        if (take is < 1 or > 100) take = 25;

        if (!TryParseScope(scope, out var parsedScope, out var error))
        {
            return ValidationProblem(error);
        }

        var now = DateTime.UtcNow;
        var query = _db.Elections.AsQueryable().Where(e => e.ScheduledAt >= now);

        if (parsedScope is not null) query = query.Where(e => e.Scope == parsedScope);
        if (!string.IsNullOrWhiteSpace(region)) query = query.Where(e => e.Region == region);

        var items = await query
            .OrderBy(e => e.ScheduledAt)
            .Take(take)
            .ToListAsync();

        return Ok(items.Select(e => e.ToDto()));
    }

    // GET /api/elections/next?scope=national&region=CA
    // Returns the single next-upcoming election, optionally filtered by scope/region.
    // 404 if none are upcoming for the given filter.
    [HttpGet("next")]
    public async Task<ActionResult<ElectionDto>> Next(
        [FromQuery] string? scope,
        [FromQuery] string? region)
    {
        if (!TryParseScope(scope, out var parsedScope, out var error))
        {
            return ValidationProblem(error);
        }

        var now = DateTime.UtcNow;
        var query = _db.Elections.AsQueryable().Where(e => e.ScheduledAt >= now);

        if (parsedScope is not null) query = query.Where(e => e.Scope == parsedScope);
        if (!string.IsNullOrWhiteSpace(region)) query = query.Where(e => e.Region == region);

        var next = await query.OrderBy(e => e.ScheduledAt).FirstOrDefaultAsync();
        return next is null ? NotFound() : Ok(next.ToDto());
    }

    private static bool TryParseScope(string? raw, out ElectionScope? parsed, out string error)
    {
        parsed = null;
        error = "";
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var normalized = raw.Replace("_", "").Replace("-", "");
        if (Enum.TryParse<ElectionScope>(normalized, ignoreCase: true, out var p))
        {
            parsed = p;
            return true;
        }
        error = $"Unknown election scope '{raw}'.";
        return false;
    }
}
