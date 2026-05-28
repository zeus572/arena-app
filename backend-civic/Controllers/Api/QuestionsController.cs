using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private readonly CivicDbContext _db;

    public QuestionsController(CivicDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<QuestionDto>>> List(
        [FromQuery] string? type,
        [FromQuery] int take = 50)
    {
        if (take is < 1 or > 200) take = 50;

        var query = _db.CivicQuestions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalized = type.Replace("_", "").Replace("-", "");
            if (!Enum.TryParse<CivicQuestionType>(normalized, ignoreCase: true, out var parsed))
            {
                return ValidationProblem($"Unknown question type '{type}'.");
            }
            query = query.Where(q => q.Type == parsed);
        }

        var items = await query
            .OrderBy(q => q.Order)
            .Take(take)
            .ToListAsync();

        return Ok(items.Select(q => q.ToDto()));
    }
}
