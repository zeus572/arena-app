using System.Text.Json.Nodes;
using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Models.Daily;
using Civic.API.Services;
using Civic.API.Services.Daily;
using Civic.API.Services.Daily.Generators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// The daily-games review queue.
///
/// Fork and Time Machine generate into <see cref="DailyPuzzleStatus.Draft"/> because a bad
/// puzzle in either is publicly visible and can read as partisan — a "would you rather"
/// whose options aren't equally costly, or a juxtaposition of headlines that implies an
/// argument no single headline makes. Without this queue those two games could never go
/// live at all.
///
/// Also surfaces the bank-balance report the gamification docs commit to (a monthly bias
/// audit): a puzzle bank is an editorial position whether or not anyone intended one.
///
/// Gated by the "Admin" policy (email allowlist in Auth:AdminEmails).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/admin/daily")]
public class AdminDailyController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;

    public AdminDailyController(CivicDbContext db, ICivicCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    /// <summary>The generated buffer, newest first. Payloads include the answer key —
    /// this is a reviewer's view, not a player's.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminDailyPuzzleDto>>> List(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = _db.DailyPuzzles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<DailyPuzzleStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(p => p.PuzzleDate)
            .ThenBy(p => p.Kind)
            .Take(120)
            .ToListAsync(ct);

        var playCounts = await _db.DailyPuzzlePlays
            .Where(p => p.Completed)
            .GroupBy(p => p.PuzzleId)
            .Select(g => new { PuzzleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PuzzleId, x => x.Count, ct);

        return Ok(rows.Select(p => new AdminDailyPuzzleDto
        {
            Id = p.Id,
            Kind = p.Kind.ToString(),
            PuzzleDate = p.PuzzleDate.ToString("yyyy-MM-dd"),
            Edition = p.Edition,
            Status = p.Status.ToString(),
            GenerationSource = p.GenerationSource,
            Locality = p.Locality,
            Plays = playCounts.GetValueOrDefault(p.Id),
            Payload = JsonNode.Parse(p.PayloadJson),
        }).ToList());
    }

    /// <summary>Approve a draft — it goes live on its puzzle date.</summary>
    [HttpPost("{id:guid}/approve")]
    public Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        SetStatusAsync(id, DailyPuzzleStatus.Live, ct);

    /// <summary>
    /// Reject a puzzle. Retired rather than deleted so the generator's
    /// "don't reuse this source" checks still see it and won't immediately re-cut the
    /// same puzzle from the same content tomorrow.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public Task<IActionResult> Reject(Guid id, CancellationToken ct) =>
        SetStatusAsync(id, DailyPuzzleStatus.Retired, ct);

    private async Task<IActionResult> SetStatusAsync(Guid id, DailyPuzzleStatus status, CancellationToken ct)
    {
        var puzzle = await _db.DailyPuzzles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (puzzle is null) return NotFound();

        puzzle.Status = status;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Bank-balance audit. A magnitude bank stacked with "much smaller than you think"
    /// answers argues a thesis; a Fork bank where the same axis pole always wins is an
    /// editorial position. Surface both so drift is visible.
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<AdminDailyBalanceDto>> Balance(CancellationToken ct)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

        var forkPuzzles = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.Fork && p.PuzzleDate >= since)
            .ToListAsync(ct);

        var forkAxisCounts = new Dictionary<string, int>();
        foreach (var p in forkPuzzles)
        {
            try
            {
                var axisKey = DailyJson.Deserialize<ForkPayload>(p.PayloadJson).AxisKey;
                var name = _catalog.AxisFor(axisKey)?.Name ?? axisKey;
                forkAxisCounts[name] = forkAxisCounts.GetValueOrDefault(name) + 1;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                // An older payload shape shouldn't break the audit view.
            }
        }

        var magnitudes = SeedService.LoadJson<List<JsonObject>>("Seed.magnitudes.json") ?? new();
        var verified = magnitudes
            .Where(m => m["verified"]?.GetValue<bool>() == true)
            .ToList();
        var smaller = verified.Count(m => m["direction"]?.GetValue<string>() == "smaller");

        var staleCutoff = DateTime.UtcNow.AddMonths(-PricedInGenerator.StaleAfterMonths);
        var stale = verified
            .Where(m => DateTime.TryParse(m["asOf"]?.GetValue<string>(), out var asOf) && asOf < staleCutoff)
            .Select(m => m["key"]?.GetValue<string>() ?? "")
            .ToList();

        return Ok(new AdminDailyBalanceDto
        {
            ForkAxisCounts = forkAxisCounts,
            MagnitudeTotal = verified.Count,
            MagnitudeSmallerCount = smaller,
            MagnitudeSmallerShare = verified.Count == 0 ? 0 : (double)smaller / verified.Count,
            StaleMagnitudeKeys = stale,
        });
    }
}
