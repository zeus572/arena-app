using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/answers")]
public class AnswersController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IProfileScoringService _scoring;

    public AnswersController(
        CivicDbContext db,
        ICurrentUserService user,
        IProfileScoringService scoring)
    {
        _db = db;
        _user = user;
        _scoring = scoring;
    }

    [HttpPost]
    public async Task<ActionResult<AnswerDto>> Submit([FromBody] CreateAnswerRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!Enum.TryParse<AnswerConfidence>(req.Confidence, ignoreCase: true, out var conf))
        {
            return ValidationProblem($"Unknown confidence '{req.Confidence}'.");
        }
        if (!Enum.TryParse<AnswerIntensity>(req.Intensity, ignoreCase: true, out var inten))
        {
            return ValidationProblem($"Unknown intensity '{req.Intensity}'.");
        }

        var question = await _db.CivicQuestions
            .FirstOrDefaultAsync(q => q.Id == req.QuestionId);
        if (question is null) return NotFound($"Question {req.QuestionId} not found.");

        if (!question.Choices.Any(c => c.Key == req.SelectedChoiceKey))
        {
            return ValidationProblem(
                $"Choice '{req.SelectedChoiceKey}' is not valid for question {question.ExternalId}.");
        }

        var userId = _user.GetCurrentUserId();
        var now = DateTime.UtcNow;

        var existing = await _db.CivicAnswers
            .FirstOrDefaultAsync(a => a.UserId == userId && a.QuestionId == req.QuestionId);

        if (existing is null)
        {
            existing = new CivicAnswer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuestionId = req.QuestionId,
                CreatedAt = now,
            };
            _db.CivicAnswers.Add(existing);
        }

        existing.SelectedChoiceKey = req.SelectedChoiceKey;
        existing.Confidence = conf;
        existing.Intensity = inten;
        existing.ReasoningChoice = req.ReasoningChoice;
        existing.FreeTextReasoning = req.FreeTextReasoning;
        existing.UpdatedAt = now;

        await _db.SaveChangesAsync();

        // Recompute profile on every answer so /api/profile/me stays fresh.
        await _scoring.RecomputeAsync(userId);

        return Ok(existing.ToDto(question.ExternalId));
    }

    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<AnswerDto>>> Mine()
    {
        var userId = _user.GetCurrentUserId();
        var items = await _db.CivicAnswers
            .Where(a => a.UserId == userId)
            .Include(a => a.Question)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
        return Ok(items.Select(a => a.ToDto(a.Question?.ExternalId ?? "")));
    }
}
