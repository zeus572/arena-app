using Civic.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services;

/// <summary>
/// The quiz "global poll": what share of people got each question right, as a trailing
/// moving average.
///
/// Extracted from QuizController so the Crowd Call daily game can read the same figure.
/// There must be exactly one definition of the window — if the game and the quiz page
/// disagree about the same question on the same day, that discrepancy is public.
/// </summary>
public class QuizPollStats
{
    /// <summary>Trailing window for the moving average. The single source of truth.</summary>
    public const int WindowDays = 60;

    private readonly CivicDbContext _db;

    public QuizPollStats(CivicDbContext db) => _db = db;

    /// <summary>
    /// Tallies keyed by question id. Pass a set of ids to scope the query, or null for
    /// the whole bank. Questions with no responses in the window are simply absent.
    /// </summary>
    public async Task<Dictionary<Guid, (int Total, int Correct)>> ForQuestionsAsync(
        IReadOnlyCollection<Guid>? questionIds, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-WindowDays);
        var query = _db.QuizResponses.Where(r => r.CreatedAt >= cutoff);

        if (questionIds is not null)
        {
            if (questionIds.Count == 0) return new();
            var ids = questionIds.ToList();
            query = query.Where(r => ids.Contains(r.QuestionId));
        }

        var rows = await query
            .GroupBy(r => r.QuestionId)
            .Select(g => new { QuestionId = g.Key, Total = g.Count(), Correct = g.Count(r => r.IsCorrect) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.QuestionId, r => (r.Total, r.Correct));
    }
}
