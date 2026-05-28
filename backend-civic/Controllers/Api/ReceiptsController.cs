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
[Route("api/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IReceiptService _receipts;

    public ReceiptsController(
        CivicDbContext db,
        ICurrentUserService user,
        IReceiptService receipts)
    {
        _db = db;
        _user = user;
        _receipts = receipts;
    }

    [HttpPost]
    public async Task<ActionResult<ValuesReceiptDto>> Build()
    {
        var userId = _user.GetCurrentUserId();
        var receipt = await _receipts.BuildAsync(userId);
        return Ok(ToDto(receipt));
    }

    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<ValuesReceiptDto>>> Mine(
        [FromQuery] int take = 10)
    {
        if (take is < 1 or > 50) take = 10;
        var userId = _user.GetCurrentUserId();
        var items = await _db.ValuesReceipts
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValuesReceiptDto>> Get(Guid id)
    {
        var userId = _user.GetCurrentUserId();
        var receipt = await _db.ValuesReceipts
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
        return receipt is null ? NotFound() : Ok(ToDto(receipt));
    }

    private static ValuesReceiptDto ToDto(ValuesReceipt r) => new()
    {
        Id = r.Id,
        CreatedAt = r.CreatedAt,
        AnswerCountAtTime = r.AnswerCountAtTime,
        ProfileVersionAtTime = r.ProfileVersionAtTime,
        LearnedInsights = r.LearnedInsights,
        ChangedAxes = r.ChangedAxes,
        UncertainAreas = r.UncertainAreas,
        Tensions = r.Tensions
            .Select(t => new ReceiptTensionDto
            {
                AxisKey = t.AxisKey,
                AxisName = t.AxisName,
                Framing = t.Framing,
            })
            .ToList(),
    };
}
