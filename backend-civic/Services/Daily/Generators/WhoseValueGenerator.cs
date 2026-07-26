using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Whose Value (06): read an argument, name the value it appeals to.
///
/// The best reuse in the set — <see cref="BillAxisPosition.Rationale"/> IS the argument
/// and <c>AxisKey</c> IS the answer, so there is no authoring and no LLM. The work is all
/// in filtering: rationales that name their own axis are rejected (see
/// <see cref="AxisLeakFilter"/>), and distractors are drawn from axes the same bill also
/// touches so they're genuinely tempting rather than absurd.
/// </summary>
public class WhoseValueGenerator : IDailyPuzzleGenerator
{
    /// <summary>Higher than Place It's bar — here a wrong label is unambiguously wrong.</summary>
    public const double MinConfidence = 0.7;
    public const int RoundsPerPuzzle = 5;
    public const int ChoicesPerRound = 4;

    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;
    private readonly ILogger<WhoseValueGenerator> _logger;

    public WhoseValueGenerator(CivicDbContext db, ICivicCatalog catalog, ILogger<WhoseValueGenerator> logger)
    {
        _db = db;
        _catalog = catalog;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.WhoseValue;
    public bool RequiresReview => false;

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var usedPairs = await UsedPairsAsync(ct);

        var bills = await _db.Bills
            .Include(b => b.AxisPositions)
            .Where(b => b.SynthesisStatus == BillSynthesisStatus.Synthesized)
            .OrderByDescending(b => b.LatestActionDate ?? b.IntroducedDate)
            .Take(200)
            .ToListAsync(ct);

        var rounds = new List<WhoseValueRound>();
        var usedBills = new HashSet<Guid>();
        var usedAxes = new HashSet<string>();
        var rejectedForLeak = 0;

        foreach (var bill in bills)
        {
            if (rounds.Count >= RoundsPerPuzzle) break;
            if (usedBills.Contains(bill.Id)) continue;

            foreach (var position in bill.AxisPositions.Where(a => a.Confidence >= MinConfidence)
                                                      .OrderByDescending(a => a.Confidence))
            {
                if (usedAxes.Contains(position.AxisKey)) continue;
                if (usedPairs.Contains((bill.Id, position.AxisKey))) continue;

                var def = _catalog.AxisFor(position.AxisKey);
                if (def is null) continue;

                if (AxisLeakFilter.Leaks(position.Rationale, def.Name, def.LowLabel, def.HighLabel))
                {
                    rejectedForLeak++;
                    continue;
                }

                var choices = BuildChoices(bill, position.AxisKey, date, rounds.Count);
                if (choices.Count < ChoicesPerRound) continue;

                rounds.Add(new WhoseValueRound(
                    position.Rationale,
                    bill.ShortTitle ?? bill.Title,
                    bill.Id,
                    choices,
                    position.AxisKey));

                usedBills.Add(bill.Id);
                usedAxes.Add(position.AxisKey);
                break; // one round per bill
            }
        }

        if (rounds.Count < RoundsPerPuzzle)
        {
            _logger.LogInformation(
                "Whose Value: only {Found}/{Needed} eligible rounds for {Date} ({Rejected} rejected as axis leaks)",
                rounds.Count, RoundsPerPuzzle, date, rejectedForLeak);
            return null;
        }

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(new WhoseValuePayload(rounds)),
            GenerationSource = CivicGenerationSource.News,
        };
    }

    /// <summary>
    /// The correct axis plus three distractors, preferring other axes the same bill
    /// touches. Shuffled HERE, not at render, so the client can't infer the answer from
    /// ordering. The shuffle is seeded off the date + round index so a regenerated puzzle
    /// is byte-identical (Random.Shared would make generation non-reproducible).
    /// </summary>
    private List<WhoseValueChoice> BuildChoices(Bill bill, string correctAxisKey, DateOnly date, int roundIndex)
    {
        var sameBillAxes = bill.AxisPositions
            .Select(a => a.AxisKey)
            .Where(k => k != correctAxisKey)
            .Distinct()
            .ToList();

        var otherAxes = _catalog.Axes
            .Select(a => a.Key)
            .Where(k => k != correctAxisKey && !sameBillAxes.Contains(k))
            .ToList();

        var rng = new Random(HashCode.Combine(date.DayNumber, roundIndex, correctAxisKey));
        Shuffle(sameBillAxes, rng);
        Shuffle(otherAxes, rng);

        var keys = new List<string> { correctAxisKey };
        keys.AddRange(sameBillAxes.Take(ChoicesPerRound - 1));
        if (keys.Count < ChoicesPerRound)
            keys.AddRange(otherAxes.Take(ChoicesPerRound - keys.Count));

        Shuffle(keys, rng);

        return keys
            .Select(k => _catalog.AxisFor(k))
            .Where(d => d is not null)
            .Select(d => new WhoseValueChoice(d!.Key, d.Name, d.LowLabel, d.HighLabel))
            .ToList();
    }

    private async Task<HashSet<(Guid, string)>> UsedPairsAsync(CancellationToken ct)
    {
        var payloads = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.WhoseValue)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var used = new HashSet<(Guid, string)>();
        foreach (var json in payloads)
        {
            try
            {
                foreach (var r in DailyJson.Deserialize<WhoseValuePayload>(json).Rounds)
                    used.Add((r.BillId, r.CorrectAxisKey));
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                // A payload from an older shape shouldn't stop today's generation.
                _logger.LogWarning(ex, "Whose Value: skipping unreadable historical payload");
            }
        }
        return used;
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
