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
[Route("api/election")]
public class ElectionCyclesController : ControllerBase
{
    private readonly CivicDbContext _db;

    public ElectionCyclesController(CivicDbContext db) => _db = db;

    // GET /api/election/cycles/current
    [HttpGet("cycles/current")]
    public async Task<ActionResult<ElectionCycleDto>> Current()
    {
        var cycle = await _db.ElectionCycles
            .Where(c => c.IsCurrent)
            .OrderByDescending(c => c.ElectionDate)
            .FirstOrDefaultAsync();
        return cycle is null ? NotFound() : Ok(cycle.ToDto());
    }

    // GET /api/election/races?office=Senate&state=CA — candidates grouped by seat.
    [HttpGet("races")]
    public async Task<ActionResult<IEnumerable<RaceDto>>> Races(
        [FromQuery] string? office,
        [FromQuery] string? state,
        [FromQuery] int? district)
    {
        var query = _db.VirtualCandidates.AsQueryable();
        if (Enum.TryParse<CandidateOffice>(office, ignoreCase: true, out var parsedOffice))
            query = query.Where(c => c.Office == parsedOffice);
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(c => c.State == state);
        if (district is not null)
            query = query.Where(c => c.District == district);

        var candidates = await query.OrderBy(c => c.Name).ToListAsync();

        var races = candidates
            .GroupBy(c => new { c.Office, c.State, c.District })
            .Select(g => new RaceDto
            {
                Office = g.Key.Office.ToString(),
                State = g.Key.State,
                District = g.Key.District,
                Label = RaceLabel(g.Key.Office, g.Key.State, g.Key.District),
                Candidates = g.Select(c => c.ToSummaryDto()).ToList(),
            })
            .OrderBy(r => r.Office)
            .ThenBy(r => r.State)
            .ThenBy(r => r.District)
            .ToList();

        return Ok(races);
    }

    private static string RaceLabel(CandidateOffice office, string? state, int? district) => office switch
    {
        CandidateOffice.President => "President of the United States",
        CandidateOffice.Senate => $"U.S. Senate — {state}",
        CandidateOffice.House => $"U.S. House — {state}-{district}",
        _ => office.ToString(),
    };
}
