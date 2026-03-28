using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public ProfileController(ArenaDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.PoliticalLeaning,
            Plan = user.Plan.ToString(),
            user.EmailVerified,
            user.AuthProvider,
            user.Xp,
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName.Trim();
        if (request.PoliticalLeaning is not null)
            user.PoliticalLeaning = request.PoliticalLeaning.Trim();
        if (request.AvatarUrl is not null)
            user.AvatarUrl = request.AvatarUrl.Trim();

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.PoliticalLeaning,
            Plan = user.Plan.ToString(),
            user.EmailVerified,
        });
    }
    [HttpGet("me/stats")]
    public async Task<IActionResult> GetStats()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        var voteCount = await _db.Votes.CountAsync(v => v.UserId == userId);
        var reactionCount = await _db.Reactions.CountAsync(r => r.UserId == userId);
        var debatesStarted = await _db.Debates.CountAsync(d => d.StartedByUserId == userId);
        var predictionsCount = await _db.Predictions.CountAsync(p => p.UserId == userId);
        var correctPredictions = await _db.Predictions.CountAsync(p => p.UserId == userId && p.IsCorrect == true);
        var interventionsCount = await _db.Interventions.CountAsync(i => i.UserId == userId);

        // Compute XP: 10 per vote, 5 per reaction, 50 per debate started, 20 per prediction, 30 per correct prediction, 15 per intervention
        var computedXp = (voteCount * 10) + (reactionCount * 5) + (debatesStarted * 50) +
                         (predictionsCount * 20) + (correctPredictions * 30) + (interventionsCount * 15);

        // Update stored XP if changed
        if (user.Xp != computedXp)
        {
            user.Xp = computedXp;
            await _db.SaveChangesAsync();
        }

        // Level: sqrt(xp / 100), min level 1
        var level = Math.Max(1, (int)Math.Floor(Math.Sqrt(computedXp / 100.0)) + 1);
        var xpForCurrentLevel = (int)Math.Pow(level - 1, 2) * 100;
        var xpForNextLevel = (int)Math.Pow(level, 2) * 100;
        var xpProgress = xpForNextLevel > xpForCurrentLevel
            ? (double)(computedXp - xpForCurrentLevel) / (xpForNextLevel - xpForCurrentLevel) * 100
            : 0;

        // Title based on level
        var title = level switch
        {
            >= 20 => "Arena Legend",
            >= 15 => "Master Debater",
            >= 10 => "Senior Analyst",
            >= 7 => "Political Junkie",
            >= 5 => "Active Citizen",
            >= 3 => "Engaged Voter",
            _ => "Newcomer",
        };

        // Badges
        var badges = new List<object>();
        if (voteCount >= 1) badges.Add(new { id = "first_vote", name = "First Vote", icon = "vote", description = "Cast your first vote" });
        if (voteCount >= 10) badges.Add(new { id = "avid_voter", name = "Avid Voter", icon = "vote", description = "Cast 10 votes" });
        if (voteCount >= 50) badges.Add(new { id = "ballot_master", name = "Ballot Master", icon = "vote", description = "Cast 50 votes" });
        if (reactionCount >= 5) badges.Add(new { id = "reactor", name = "Reactor", icon = "reaction", description = "Left 5 reactions" });
        if (reactionCount >= 25) badges.Add(new { id = "super_reactor", name = "Super Reactor", icon = "reaction", description = "Left 25 reactions" });
        if (debatesStarted >= 1) badges.Add(new { id = "debate_starter", name = "Debate Starter", icon = "debate", description = "Started your first debate" });
        if (debatesStarted >= 5) badges.Add(new { id = "provocateur", name = "Provocateur", icon = "debate", description = "Started 5 debates" });
        if (predictionsCount >= 1) badges.Add(new { id = "fortune_teller", name = "Fortune Teller", icon = "predict", description = "Made your first prediction" });
        if (correctPredictions >= 3) badges.Add(new { id = "oracle", name = "Oracle", icon = "predict", description = "3 correct predictions" });
        if (correctPredictions >= 10) badges.Add(new { id = "prophet", name = "Prophet", icon = "predict", description = "10 correct predictions" });
        if (interventionsCount >= 1) badges.Add(new { id = "questioner", name = "Questioner", icon = "question", description = "Asked your first crowd question" });

        return Ok(new
        {
            xp = computedXp,
            level,
            title,
            xpProgress = Math.Round(xpProgress, 1),
            xpForNextLevel,
            activity = new
            {
                votes = voteCount,
                reactions = reactionCount,
                debatesStarted,
                predictions = predictionsCount,
                correctPredictions,
                interventions = interventionsCount,
            },
            badges,
        });
    }
}

public record UpdateProfileRequest(string? DisplayName, string? PoliticalLeaning, string? AvatarUrl);
