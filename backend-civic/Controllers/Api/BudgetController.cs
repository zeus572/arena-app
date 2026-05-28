using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/budget")]
public class BudgetController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ICivicCatalog _catalog;
    private readonly IProfileScoringService _scoring;

    public BudgetController(
        CivicDbContext db,
        ICurrentUserService user,
        ICivicCatalog catalog,
        IProfileScoringService scoring)
    {
        _db = db;
        _user = user;
        _catalog = catalog;
        _scoring = scoring;
    }

    [HttpGet("categories")]
    public ActionResult<IEnumerable<BudgetCategoryDto>> Categories()
    {
        var items = _catalog.BudgetCategories
            .OrderBy(c => c.Order)
            .Select(c => new BudgetCategoryDto
            {
                Key = c.Key,
                Name = c.Name,
                Description = c.Description,
                Order = c.Order,
            });
        return Ok(items);
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<BudgetSessionDto>> Start()
    {
        var userId = _user.GetCurrentUserId();
        var now = DateTime.UtcNow;
        var session = new BudgetSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.BudgetSessions.Add(session);
        await _db.SaveChangesAsync();
        return Ok(ToDto(session));
    }

    [HttpGet("sessions/me/current")]
    public async Task<ActionResult<BudgetSessionDto?>> Current()
    {
        var userId = _user.GetCurrentUserId();
        var session = await _db.BudgetSessions
            .Where(s => s.UserId == userId && s.CompletedAt == null)
            .Include(s => s.Allocations)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        return session is null ? Ok(null) : Ok(ToDto(session));
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<BudgetSessionDto>> Get(Guid id)
    {
        var userId = _user.GetCurrentUserId();
        var session = await _db.BudgetSessions
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        return session is null ? NotFound() : Ok(ToDto(session));
    }

    [HttpPut("sessions/{id:guid}/allocations")]
    public async Task<ActionResult<BudgetSessionDto>> SetAllocations(
        Guid id,
        [FromBody] SetAllocationsRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = _user.GetCurrentUserId();
        var session = await _db.BudgetSessions
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session is null) return NotFound();
        if (session.CompletedAt is not null)
        {
            return ValidationProblem("Session is already completed and cannot be edited.");
        }

        foreach (var a in req.Allocations)
        {
            if (_catalog.BudgetCategoryFor(a.CategoryKey) is null)
            {
                return ValidationProblem($"Unknown budget category '{a.CategoryKey}'.");
            }
            if (a.Points is < 0 or > 100)
            {
                return ValidationProblem($"Points for '{a.CategoryKey}' must be in [0, 100].");
            }
        }

        _db.BudgetAllocations.RemoveRange(session.Allocations);
        session.Allocations.Clear();
        await _db.SaveChangesAsync();

        foreach (var a in req.Allocations)
        {
            var row = new BudgetAllocation
            {
                Id = Guid.NewGuid(),
                BudgetSessionId = session.Id,
                CategoryKey = a.CategoryKey,
                Points = a.Points,
            };
            session.Allocations.Add(row);
            _db.BudgetAllocations.Add(row);
        }
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(session));
    }

    [HttpPost("sessions/{id:guid}/complete")]
    public async Task<ActionResult<BudgetSessionDto>> Complete(Guid id)
    {
        var userId = _user.GetCurrentUserId();
        var session = await _db.BudgetSessions
            .Include(s => s.Allocations)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (session is null) return NotFound();
        if (session.CompletedAt is not null) return Ok(ToDto(session));

        var sum = session.Allocations.Sum(a => a.Points);
        if (sum != 100)
        {
            return ValidationProblem(
                $"Budget must sum to exactly 100 points. Current total: {sum}.");
        }

        session.CompletedAt = DateTime.UtcNow;
        session.UpdatedAt = session.CompletedAt.Value;
        await _db.SaveChangesAsync();

        await _scoring.RecomputeAsync(userId);

        return Ok(ToDto(session));
    }

    private static BudgetSessionDto ToDto(BudgetSession s) => new()
    {
        Id = s.Id,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        CompletedAt = s.CompletedAt,
        TotalPoints = s.Allocations.Sum(a => a.Points),
        IsComplete = s.CompletedAt is not null,
        Allocations = s.Allocations
            .Select(a => new BudgetAllocationDto
            {
                CategoryKey = a.CategoryKey,
                Points = a.Points,
            })
            .OrderBy(a => a.CategoryKey)
            .ToList(),
    };
}
