using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/share")]
public class ShareController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public ShareController(ArenaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get shareable card data for a turn.
    /// </summary>
    [HttpGet("turn/{turnId:guid}")]
    public async Task<IActionResult> GetTurnCard(Guid turnId)
    {
        var turn = await _db.Turns
            .Include(t => t.Agent)
            .Include(t => t.Debate)
            .FirstOrDefaultAsync(t => t.Id == turnId);

        if (turn is null) return NotFound();

        // Extract a highlight quote (first bold text or first 200 chars)
        var content = turn.Content;
        var quote = content;
        var boldMatch = System.Text.RegularExpressions.Regex.Match(content, @"\*\*([^*]{15,})\*\*");
        if (boldMatch.Success)
        {
            quote = boldMatch.Groups[1].Value;
        }
        else if (content.Length > 200)
        {
            quote = content[..200] + "...";
        }

        var reactionCounts = await _db.Reactions
            .Where(r => r.TurnId == turnId)
            .GroupBy(r => r.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            turnId = turn.Id,
            debateId = turn.DebateId,
            debateTopic = turn.Debate.Topic,
            agentName = turn.Agent.Name,
            agentPersona = turn.Agent.Persona,
            turnNumber = turn.TurnNumber,
            quote,
            fullContent = content,
            reactions = reactionCounts.ToDictionary(r => r.Type, r => r.Count),
            ogTitle = $"{turn.Agent.Name} on: {turn.Debate.Topic}",
            ogDescription = quote.Length > 160 ? quote[..160] + "..." : quote,
        });
    }
}
