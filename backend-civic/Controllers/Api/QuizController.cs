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
[Route("api/quiz")]
public class QuizController : ControllerBase
{
    /// <summary>Trailing window for the global poll's "got it right" moving average.</summary>
    public const int PollWindowDays = 60;
    /// <summary>Default number of questions served per quiz session (kept dynamic by shuffling).</summary>
    private const int DefaultCount = 6;

    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;

    public QuizController(CivicDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>
    /// Returns a freshly shuffled subset of the question bank so the quiz isn't the same every
    /// time, each carrying the global poll stats (60-day moving average of correct answers).
    /// </summary>
    [HttpGet("questions")]
    public async Task<ActionResult<IEnumerable<QuizQuestionDto>>> List([FromQuery] int? count)
    {
        var questions = await _db.QuizQuestions.ToListAsync();
        var stats = await PollStatsAsync(null);

        var take = count is > 0 ? count.Value : DefaultCount;
        // Shuffle the bank and take a subset → dynamic question set on every load.
        var selected = questions
            .OrderBy(_ => Random.Shared.Next())
            .Take(Math.Min(take, questions.Count))
            .ToList();

        var dtos = selected.Select(q =>
        {
            var dto = q.ToDto();
            if (stats.TryGetValue(q.Id, out var s))
            {
                dto.ResponseCount = s.Total;
                dto.CorrectRate = s.Total == 0 ? 0 : (double)s.Correct / s.Total;
            }
            return dto;
        });
        return Ok(dtos);
    }

    /// <summary>
    /// Records one person's answer and returns the updated global poll for that question.
    /// </summary>
    [HttpPost("questions/{id:guid}/responses")]
    public async Task<ActionResult<QuizPollResultDto>> Respond(Guid id, [FromBody] QuizResponseRequest req)
    {
        var question = await _db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == id);
        if (question is null) return NotFound();

        if (req.SelectedIndex < 0 || req.SelectedIndex >= question.Options.Length)
            return BadRequest($"SelectedIndex must be between 0 and {question.Options.Length - 1}.");

        var isCorrect = req.SelectedIndex == question.CorrectAnswerIndex;
        _db.QuizResponses.Add(new QuizResponse
        {
            Id = Guid.NewGuid(),
            QuestionId = id,
            UserId = _user.GetCurrentUserId(),
            SelectedIndex = req.SelectedIndex,
            IsCorrect = isCorrect,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var stats = await PollStatsAsync(id);
        stats.TryGetValue(id, out var s);
        return Ok(new QuizPollResultDto
        {
            QuestionId = id,
            CorrectAnswerIndex = question.CorrectAnswerIndex,
            IsCorrect = isCorrect,
            ResponseCount = s.Total,
            CorrectCount = s.Correct,
            CorrectRate = s.Total == 0 ? 0 : (double)s.Correct / s.Total,
            WindowDays = PollWindowDays,
        });
    }

    /// <summary>
    /// 60-day moving-average poll tallies, keyed by question id. Pass a question id to scope the
    /// query, or null for the whole bank.
    /// </summary>
    private async Task<Dictionary<Guid, (int Total, int Correct)>> PollStatsAsync(Guid? questionId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-PollWindowDays);
        var query = _db.QuizResponses.Where(r => r.CreatedAt >= cutoff);
        if (questionId is not null) query = query.Where(r => r.QuestionId == questionId.Value);

        var rows = await query
            .GroupBy(r => r.QuestionId)
            .Select(g => new { QuestionId = g.Key, Total = g.Count(), Correct = g.Count(r => r.IsCorrect) })
            .ToListAsync();

        return rows.ToDictionary(r => r.QuestionId, r => (r.Total, r.Correct));
    }
}
