using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/budget-facts")]
public class BudgetFactsController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public BudgetFactsController(ArenaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Today's "Did You Know?" budget contradictions, falling back to the most
    /// recent batch if today's haven't been generated yet.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTodaysFacts()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var facts = await _db.BudgetFacts
            .Where(f => f.FactDate == today && f.IsActive)
            .OrderBy(f => f.GeneratedAt)
            .ToListAsync();

        if (facts.Count == 0)
        {
            var latestDate = await _db.BudgetFacts
                .Where(f => f.IsActive)
                .Select(f => (DateOnly?)f.FactDate)
                .MaxAsync();

            if (latestDate.HasValue)
            {
                facts = await _db.BudgetFacts
                    .Where(f => f.FactDate == latestDate.Value && f.IsActive)
                    .OrderBy(f => f.GeneratedAt)
                    .ToListAsync();
            }
        }

        return Ok(facts.Select(f => new
        {
            f.Id,
            f.FactDate,
            f.Category,
            f.TensionLabel,
            f.PerspectiveA,
            f.SourceA,
            f.SourceUrlA,
            f.PerspectiveB,
            f.SourceB,
            f.SourceUrlB,
            f.Explanation,
        }));
    }
}
