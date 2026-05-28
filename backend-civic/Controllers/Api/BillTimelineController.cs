using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/bill-timeline")]
public class BillTimelineController : ControllerBase
{
    private readonly CivicDbContext _db;

    public BillTimelineController(CivicDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BillTimelineStepDto>>> List()
    {
        var items = await _db.BillTimelineSteps
            .OrderBy(s => s.Order)
            .ToListAsync();
        return Ok(items.Select(s => s.ToDto()));
    }
}
