using Microsoft.AspNetCore.Mvc;
using Arena.API.Models;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class FormatsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        var formats = DebateFormatConfig.All.Values.Select(f => new
        {
            f.Format,
            f.DisplayName,
            f.Description,
            f.MaxTurns,
            f.MaxTokens,
            f.MaxCharactersPerTurn,
            f.HasCompromisePhase,
            f.HasWildcards,
            f.HasCommentary,
            f.HasTools,
            f.HasBudgetTable,
        });

        return Ok(formats);
    }
}
