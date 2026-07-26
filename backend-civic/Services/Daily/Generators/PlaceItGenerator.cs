using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Place It (04): guess where a real bill sits on three compass axes.
///
/// Pure selection over <see cref="BillAxisPosition"/> rows that BillSynthesisService has
/// already written — no LLM, no authoring. Picks the three highest-confidence axes on a
/// recently-active synthesized bill.
/// </summary>
public class PlaceItGenerator : IDailyPuzzleGenerator
{
    /// <summary>Below this the synthesis isn't sure enough to score a player against.</summary>
    public const double MinConfidence = 0.6;
    public const int AxesPerPuzzle = 3;
    public const int MaxRounds = 3;

    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;
    private readonly ILogger<PlaceItGenerator> _logger;

    public PlaceItGenerator(CivicDbContext db, ICivicCatalog catalog, ILogger<PlaceItGenerator> logger)
    {
        _db = db;
        _catalog = catalog;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.PlaceIt;

    /// <summary>Auto-approves: every row was already produced by the reviewed synthesis pipeline.</summary>
    public bool RequiresReview => false;

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var used = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.PlaceIt && p.SourceBillId != null)
            .Select(p => p.SourceBillId!.Value)
            .ToListAsync(ct);

        // Most recently active first, so the game tracks the news.
        var candidates = await _db.Bills
            .Include(b => b.AxisPositions)
            .Where(b => b.SynthesisStatus == BillSynthesisStatus.Synthesized && !used.Contains(b.Id))
            .OrderByDescending(b => b.LatestActionDate ?? b.IntroducedDate)
            .Take(50)
            .ToListAsync(ct);

        foreach (var bill in candidates)
        {
            var axes = bill.AxisPositions
                .Where(a => a.Confidence >= MinConfidence)
                .OrderByDescending(a => a.Confidence)
                .Take(AxesPerPuzzle)
                .ToList();

            if (axes.Count < AxesPerPuzzle) continue;

            var payloadAxes = new List<PlaceItAxis>();
            foreach (var axis in axes)
            {
                // Labels come from the catalog, never hard-coded — it's 15 axes and growing.
                var def = _catalog.AxisFor(axis.AxisKey);
                if (def is null)
                {
                    _logger.LogWarning("Bill {BillId} references unknown axis {AxisKey}", bill.Id, axis.AxisKey);
                    continue;
                }

                payloadAxes.Add(new PlaceItAxis(
                    axis.AxisKey, def.Name, def.LowLabel, def.HighLabel,
                    DailyScoring.BucketAxisScore(axis.Score),
                    axis.Rationale,
                    axis.Evidence));
            }

            if (payloadAxes.Count < AxesPerPuzzle) continue;

            var payload = new PlaceItPayload(
                bill.Id,
                bill.ShortTitle ?? bill.Title,
                bill.SynthesisSummary ?? bill.Summary,
                bill.Status.ToString(),
                payloadAxes,
                MaxRounds);

            return new DailyPuzzle
            {
                Kind = Kind,
                PuzzleDate = date,
                PayloadJson = DailyJson.Serialize(payload),
                SourceBillId = bill.Id,
                GenerationSource = CivicGenerationSource.News,
            };
        }

        _logger.LogInformation("Place It: no eligible synthesized bill for {Date}", date);
        return null;
    }
}
