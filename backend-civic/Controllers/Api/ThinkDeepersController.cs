using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/think-deepers")]
public class ThinkDeepersController : ControllerBase
{
    private readonly CivicDbContext _db;

    public ThinkDeepersController(CivicDbContext db) => _db = db;

    [HttpGet("{slug}")]
    public async Task<ActionResult<ThinkDeeperDto>> GetBySlug(string slug)
    {
        var t = await _db.ThinkDeepers.FirstOrDefaultAsync(x => x.Slug == slug);
        return t is null ? NotFound() : Ok(t.ToDto());
    }
}
