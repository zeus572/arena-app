using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[Route("api/petitions")]
public class PetitionsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;

    public PetitionsController(CivicDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Petition>>> List()
    {
        var items = await _db.Petitions
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Petition>> Get(Guid id)
    {
        var petition = await _db.Petitions.FindAsync(id);
        return petition is null ? NotFound() : Ok(petition);
    }

    [HttpPost]
    public async Task<ActionResult<Petition>> Create([FromBody] CreatePetitionRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var petition = new Petition
        {
            Id = Guid.NewGuid(),
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            CreatedBy = _user.GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow,
            SignatureCount = 0,
        };
        _db.Petitions.Add(petition);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = petition.Id }, petition);
    }
}
