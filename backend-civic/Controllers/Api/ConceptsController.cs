using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/concepts")]
public class ConceptsController : ControllerBase
{
    private readonly CivicDbContext _db;

    public ConceptsController(CivicDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConceptDto>>> List()
    {
        var items = await _db.Concepts
            .OrderBy(c => c.Title)
            .ToListAsync();
        return Ok(items.Select(c => c.ToDto()));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ConceptDto>> GetBySlug(string slug)
    {
        var c = await _db.Concepts.FirstOrDefaultAsync(x => x.Slug == slug);
        return c is null ? NotFound() : Ok(c.ToDto());
    }
}
