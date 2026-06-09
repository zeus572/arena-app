using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Services;

public interface IZeitgeistService
{
    Task<ZeitgeistDto> BuildAsync(CancellationToken ct = default);
}

/// <summary>
/// Aggregates "discoveries" from how people use Civersify into a single read-out: the
/// public's prevailing Civic Compass lean per axis, the coalition positions that are
/// converging, and the civic ideas people most often get wrong. Pure DB aggregation — no LLM.
/// </summary>
public class ZeitgeistService : IZeitgeistService
{
    /// <summary>A reading only counts as a "lean" past this magnitude; otherwise it's a split.</summary>
    private const double LeanThreshold = 0.15;
    private const int QuizWindowDays = 60;

    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;

    public ZeitgeistService(CivicDbContext db, ICivicCatalog catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<ZeitgeistDto> BuildAsync(CancellationToken ct = default)
    {
        var axes = await BuildAxesAsync(ct);
        var coalitions = await BuildCoalitionsAsync(ct);
        var (quizSignals, quizResponseCount) = await BuildQuizSignalsAsync(ct);

        return new ZeitgeistDto
        {
            GeneratedAt = DateTime.UtcNow,
            Totals = new ZeitgeistTotalsDto
            {
                ProfileCount = await _db.UserProfiles.CountAsync(ct),
                CoalitionCount = coalitions.Count,
                QuizResponseCount = quizResponseCount,
            },
            Axes = axes,
            Coalitions = coalitions,
            QuizSignals = quizSignals,
        };
    }

    private async Task<List<ZeitgeistAxisDto>> BuildAxesAsync(CancellationToken ct)
    {
        var rows = await _db.ProfileAxisScores
            .GroupBy(s => s.AxisKey)
            .Select(g => new { AxisKey = g.Key, Average = g.Average(x => x.Score), Count = g.Count() })
            .ToListAsync(ct);
        var byKey = rows.ToDictionary(r => r.AxisKey);

        return _catalog.Axes
            .OrderBy(a => a.Order)
            .Select(a =>
            {
                byKey.TryGetValue(a.Key, out var r);
                var avg = r?.Average ?? 0;
                var sample = r?.Count ?? 0;
                return new ZeitgeistAxisDto
                {
                    AxisKey = a.Key,
                    AxisName = a.Name,
                    LowLabel = a.LowLabel,
                    HighLabel = a.HighLabel,
                    AverageScore = avg,
                    SampleSize = sample,
                    LeanLabel = sample == 0
                        ? "No data yet"
                        : avg > LeanThreshold ? a.HighLabel
                        : avg < -LeanThreshold ? a.LowLabel
                        : "Split — no clear consensus",
                };
            })
            .ToList();
    }

    private async Task<List<ZeitgeistCoalitionDto>> BuildCoalitionsAsync(CancellationToken ct)
    {
        var provisions = await _db.Provisions.ToListAsync(ct);
        var versions = await _db.ProvisionVersions
            .Select(v => new { v.Id, v.ProvisionId, v.Label, v.Text })
            .ToListAsync(ct);
        var accRows = await _db.AcceptanceRecords
            .GroupBy(a => new { a.ProvisionId, a.VersionId })
            .Select(g => new { g.Key.ProvisionId, g.Key.VersionId, Accepts = g.Count(x => x.Accept), Declines = g.Count(x => !x.Accept) })
            .ToListAsync(ct);
        var partRows = await _db.CoalitionParticipants
            .GroupBy(p => p.ProvisionId)
            .Select(g => new { ProvisionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var accByVersion = accRows.ToDictionary(r => r.VersionId, r => (r.Accepts, r.Declines));
        var partByProvision = partRows.ToDictionary(r => r.ProvisionId, r => r.Count);

        var result = new List<ZeitgeistCoalitionDto>();
        foreach (var p in provisions)
        {
            var pVersions = versions.Where(v => v.ProvisionId == p.Id).ToList();

            // Prevailing = the version with the highest net acceptance (co-signs minus declines).
            (Guid Id, string? Label, string Text)? best = null;
            var bestNet = int.MinValue;
            var bestAccepts = -1;
            int accepts = 0, declines = 0;
            foreach (var v in pVersions)
            {
                accByVersion.TryGetValue(v.Id, out var c);
                var net = c.Accepts - c.Declines;
                if (net > bestNet || (net == bestNet && c.Accepts > bestAccepts))
                {
                    bestNet = net;
                    bestAccepts = c.Accepts;
                    best = (v.Id, v.Label, v.Text);
                    accepts = c.Accepts;
                    declines = c.Declines;
                }
            }

            var prevailing = ChoosePrevailingText(best?.Text, p.NeutralText);
            partByProvision.TryGetValue(p.Id, out var participantCount);

            result.Add(new ZeitgeistCoalitionDto
            {
                ProvisionId = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                State = p.State.ToString(),
                PrevailingPosition = prevailing,
                Accepts = accepts,
                Declines = declines,
                ParticipantCount = participantCount,
                Signal = accepts > 0
                    ? $"{accepts} co-sign{(accepts == 1 ? "" : "s")} across the spectrum — a workable position is emerging."
                    : "Still forming — no agreed wording has emerged yet.",
            });
        }

        return result
            .OrderByDescending(c => c.ParticipantCount)
            .ThenByDescending(c => c.Accepts)
            .Take(12)
            .ToList();
    }

    /// <summary>
    /// Auto-generated versions store a "Version — key = val; …" dump in Text that reads as noise;
    /// fall back to the provision's neutral wording for those.
    /// </summary>
    private static string ChoosePrevailingText(string? versionText, string neutralText)
    {
        if (string.IsNullOrWhiteSpace(versionText) || versionText.TrimStart().StartsWith("Version —"))
            return neutralText;
        return versionText.Trim();
    }

    private async Task<(List<ZeitgeistQuizSignalDto>, int)> BuildQuizSignalsAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-QuizWindowDays);
        var rows = await _db.QuizResponses
            .Where(r => r.CreatedAt >= cutoff)
            .GroupBy(r => r.QuestionId)
            .Select(g => new { QuestionId = g.Key, Total = g.Count(), Correct = g.Count(x => x.IsCorrect) })
            .ToListAsync(ct);

        var totalResponses = rows.Sum(r => r.Total);
        if (rows.Count == 0) return (new List<ZeitgeistQuizSignalDto>(), 0);

        var questions = await _db.QuizQuestions
            .Select(q => new { q.Id, q.Topic, q.Question })
            .ToListAsync(ct);
        var byId = questions.ToDictionary(q => q.Id);

        var signals = rows
            .Where(r => byId.ContainsKey(r.QuestionId))
            .Select(r => new ZeitgeistQuizSignalDto
            {
                Topic = byId[r.QuestionId].Topic,
                Question = byId[r.QuestionId].Question,
                CorrectRate = r.Total == 0 ? 0 : (double)r.Correct / r.Total,
                ResponseCount = r.Total,
            })
            .OrderBy(s => s.CorrectRate)
            .ThenByDescending(s => s.ResponseCount)
            .Take(6)
            .ToList();

        return (signals, totalResponses);
    }
}
