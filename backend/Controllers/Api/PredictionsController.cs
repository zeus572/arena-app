using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/debates/{debateId:guid}/predictions")]
public class PredictionsController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly ICurrentUserService _userService;

    public PredictionsController(ArenaDbContext db, ICurrentUserService userService)
    {
        _db = db;
        _userService = userService;
    }

    /// <summary>
    /// Get prediction odds for a debate + current user's prediction.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPredictions(Guid debateId)
    {
        var debate = await _db.Debates.FindAsync(debateId);
        if (debate is null) return NotFound();

        var predictions = await _db.Predictions
            .Where(p => p.DebateId == debateId)
            .ToListAsync();

        var proponentPredictions = predictions.Count(p => p.PredictedAgentId == debate.ProponentId);
        var opponentPredictions = predictions.Count(p => p.PredictedAgentId == debate.OpponentId);
        var total = predictions.Count;

        // Check current user's prediction
        Guid? userPredictedAgentId = null;
        bool? userIsCorrect = null;
        try
        {
            var user = await _userService.GetOrCreateUserAsync();
            var userPrediction = predictions.FirstOrDefault(p => p.UserId == user.Id);
            if (userPrediction != null)
            {
                userPredictedAgentId = userPrediction.PredictedAgentId;
                userIsCorrect = userPrediction.IsCorrect;
            }
        }
        catch { /* anonymous with no user yet */ }

        return Ok(new
        {
            totalPredictions = total,
            proponentPredictions,
            opponentPredictions,
            proponentOdds = total > 0 ? Math.Round((double)proponentPredictions / total * 100, 1) : 50.0,
            opponentOdds = total > 0 ? Math.Round((double)opponentPredictions / total * 100, 1) : 50.0,
            userPredictedAgentId,
            userIsCorrect,
        });
    }

    /// <summary>
    /// Make a prediction on who will win.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MakePrediction(Guid debateId, [FromBody] MakePredictionRequest request)
    {
        var debate = await _db.Debates.FindAsync(debateId);
        if (debate is null) return NotFound();

        // Can only predict before debate is completed
        if (debate.Status == DebateStatus.Completed)
            return BadRequest(new { error = "Cannot predict on a completed debate." });

        // Must be proponent or opponent
        if (request.PredictedAgentId != debate.ProponentId && request.PredictedAgentId != debate.OpponentId)
            return BadRequest(new { error = "Predicted agent must be one of the debate participants." });

        var user = await _userService.GetOrCreateUserAsync();

        var existing = await _db.Predictions
            .FirstOrDefaultAsync(p => p.DebateId == debateId && p.UserId == user.Id);

        if (existing != null)
        {
            // Allow changing prediction if debate isn't completed
            existing.PredictedAgentId = request.PredictedAgentId;
            await _db.SaveChangesAsync();
            return Ok(new { existing.Id, existing.PredictedAgentId });
        }

        var prediction = new Prediction
        {
            Id = Guid.NewGuid(),
            DebateId = debateId,
            UserId = user.Id,
            PredictedAgentId = request.PredictedAgentId,
        };

        _db.Predictions.Add(prediction);
        await _db.SaveChangesAsync();

        return Created("", new { prediction.Id, prediction.PredictedAgentId });
    }
}

public record MakePredictionRequest(Guid PredictedAgentId);
