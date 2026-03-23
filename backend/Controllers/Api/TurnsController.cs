using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/debates/{debateId:guid}/[controller]")]
public class TurnsController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public TurnsController(ArenaDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetByDebate(Guid debateId)
    {
        var turns = await _db.Turns
            .Where(t => t.DebateId == debateId)
            .Include(t => t.Agent)
            .OrderBy(t => t.TurnNumber)
            .ToListAsync();

        return Ok(turns);
    }
}
