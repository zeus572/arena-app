using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/quiz")]
public class QuizController : ControllerBase
{
    private readonly CivicDbContext _db;

    public QuizController(CivicDbContext db) => _db = db;

    [HttpGet("questions")]
    public async Task<ActionResult<IEnumerable<QuizQuestionDto>>> List()
    {
        var items = await _db.QuizQuestions
            .OrderBy(q => q.Order)
            .ToListAsync();
        return Ok(items.Select(q => q.ToDto()));
    }
}
