using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ICivicCatalog _catalog;
    private readonly IProfileScoringService _scoring;

    public ProfileController(
        CivicDbContext db,
        ICurrentUserService user,
        ICivicCatalog catalog,
        IProfileScoringService scoring)
    {
        _db = db;
        _user = user;
        _catalog = catalog;
        _scoring = scoring;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> Mine()
    {
        var userId = _user.GetCurrentUserId();
        var profile = await _db.UserProfiles
            .Include(p => p.AxisScores)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var answerCount = await _db.CivicAnswers.CountAsync(a => a.UserId == userId);

        // If no profile yet, return an empty profile shaped so the client can render
        // axis chrome with zero scores.
        if (profile is null)
        {
            return Ok(EmptyProfile(userId, answerCount));
        }

        return Ok(BuildDto(profile, answerCount));
    }

    [HttpPost("me/recompute")]
    public async Task<ActionResult<ProfileDto>> Recompute()
    {
        var userId = _user.GetCurrentUserId();
        var profile = await _scoring.RecomputeAsync(userId);
        var answerCount = await _db.CivicAnswers.CountAsync(a => a.UserId == userId);
        return Ok(BuildDto(profile, answerCount));
    }

    private ProfileDto EmptyProfile(string userId, int answerCount) => new()
    {
        UserId = userId,
        ProfileVersion = 0,
        UpdatedAt = DateTime.UtcNow,
        AnswerCount = answerCount,
        Axes = _catalog.Axes
            .OrderBy(a => a.Order)
            .Select(a => new AxisScoreDto
            {
                AxisKey = a.Key,
                AxisName = a.Name,
                LowLabel = a.LowLabel,
                HighLabel = a.HighLabel,
                Order = a.Order,
                Score = 0,
                Confidence = 0,
                Intensity = 0,
                SupportingAnswerCount = 0,
            })
            .ToList(),
        ArchetypeBlend = new(),
    };

    private ProfileDto BuildDto(Models.UserProfile profile, int answerCount)
    {
        var byAxisKey = profile.AxisScores.ToDictionary(s => s.AxisKey);

        var axes = _catalog.Axes
            .OrderBy(a => a.Order)
            .Select(a =>
            {
                byAxisKey.TryGetValue(a.Key, out var score);
                return new AxisScoreDto
                {
                    AxisKey = a.Key,
                    AxisName = a.Name,
                    LowLabel = a.LowLabel,
                    HighLabel = a.HighLabel,
                    Order = a.Order,
                    Score = score?.Score ?? 0,
                    Confidence = score?.Confidence ?? 0,
                    Intensity = score?.Intensity ?? 0,
                    SupportingAnswerCount = score?.SupportingAnswerIds.Length ?? 0,
                };
            })
            .ToList();

        var blend = profile.ArchetypeBlend
            .Select(b =>
            {
                var def = _catalog.ArchetypeFor(b.ArchetypeKey);
                return new ArchetypeBlendItemDto
                {
                    ArchetypeKey = b.ArchetypeKey,
                    Name = def?.Name ?? b.ArchetypeKey,
                    Description = def?.Description ?? "",
                    Percent = b.Percent,
                };
            })
            .OrderByDescending(b => b.Percent)
            .ToList();

        return new ProfileDto
        {
            UserId = profile.UserId,
            ProfileVersion = profile.ProfileVersion,
            UpdatedAt = profile.UpdatedAt,
            AnswerCount = answerCount,
            Axes = axes,
            ArchetypeBlend = blend,
        };
    }
}
