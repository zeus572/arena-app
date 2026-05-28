using Microsoft.EntityFrameworkCore;
using Civic.API.Data;
using Civic.API.Models;

namespace Civic.API.Services;

public interface IReceiptService
{
    Task<ValuesReceipt> BuildAsync(string userId, CancellationToken ct = default);
}

public class ReceiptService : IReceiptService
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;
    private readonly IExplanationService _explanation;
    private readonly IContradictionDetectionService _contradictions;
    private readonly IProfileScoringService _scoring;

    public ReceiptService(
        CivicDbContext db,
        ICivicCatalog catalog,
        IExplanationService explanation,
        IContradictionDetectionService contradictions,
        IProfileScoringService scoring)
    {
        _db = db;
        _catalog = catalog;
        _explanation = explanation;
        _contradictions = contradictions;
        _scoring = scoring;
    }

    public async Task<ValuesReceipt> BuildAsync(string userId, CancellationToken ct = default)
    {
        // Make sure the profile is fresh before we describe it.
        var profile = await _scoring.RecomputeAsync(userId, ct);

        var answers = await _db.CivicAnswers
            .Where(a => a.UserId == userId)
            .Include(a => a.Question)
            .ToListAsync(ct);

        var tensions = _contradictions.Detect(answers);
        var insights = _explanation.InsightsForProfile(profile, _catalog);
        var uncertain = _explanation.UncertainAxes(profile);

        // Compare to the previous receipt to populate ChangedAxes.
        var prior = await _db.ValuesReceipts
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        List<string> changedAxes = new();
        if (prior is not null)
        {
            // Simple change detection: axes that now have non-trivial score but were
            // previously uncertain, or vice-versa. With richer history we'd compare
            // raw scores, but those aren't stored on past receipts.
            var priorUncertain = prior.UncertainAreas.ToHashSet();
            var nowUncertain = uncertain.ToHashSet();
            changedAxes = priorUncertain.Symmetric(nowUncertain).ToList();
        }

        var receipt = new ValuesReceipt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            AnswerCountAtTime = answers.Count,
            ProfileVersionAtTime = profile.ProfileVersion,
            LearnedInsights = insights,
            ChangedAxes = changedAxes,
            UncertainAreas = uncertain,
            Tensions = tensions.Select(t =>
            {
                var axisDef = _catalog.AxisFor(t.AxisKey)
                    ?? new AxisDefinition { Key = t.AxisKey, Name = t.AxisKey };
                return new ReceiptTension
                {
                    AxisKey = t.AxisKey,
                    AxisName = t.AxisName,
                    Framing = _explanation.FrameTension(t, axisDef),
                    AnswerIdsLow = t.AnswerIdsLow,
                    AnswerIdsHigh = t.AnswerIdsHigh,
                };
            }).ToList(),
        };

        _db.ValuesReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);
        return receipt;
    }
}

internal static class SetExtensions
{
    public static IEnumerable<T> Symmetric<T>(this IEnumerable<T> a, IEnumerable<T> b)
    {
        var setA = a.ToHashSet();
        var setB = b.ToHashSet();
        var result = new HashSet<T>(setA);
        result.SymmetricExceptWith(setB);
        return result;
    }
}
