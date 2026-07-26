using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Civic.API.Services.TaxModel;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Priced In (03): guess the size of a real civic figure, three guesses, higher/lower.
///
/// Two sources:
///  - <b>Derived</b> — computed live by <see cref="TaxEngine"/> against the current
///    <see cref="TaxConstants.TaxYear"/>. These are self-refreshing: the annual constants
///    bump the tax model already documents re-prices them for free.
///  - <b>Static</b> — Seed/magnitudes.json. Every entry carries an <c>asOf</c> and a
///    <c>verified</c> flag; unverified rows never ship (a stale or invented figure
///    presented as current is the failure mode that costs the most credibility).
/// </summary>
public class PricedInGenerator : IDailyPuzzleGenerator
{
    public const int MaxGuesses = 3;

    /// <summary>Flag a static figure this old for re-verification on the admin page.</summary>
    public const int StaleAfterMonths = 24;

    private readonly CivicDbContext _db;
    private readonly ILogger<PricedInGenerator> _logger;

    public PricedInGenerator(CivicDbContext db, ILogger<PricedInGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.PricedIn;
    public bool RequiresReview => false;

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var used = await UsedPromptsAsync(ct);

        var candidates = new List<PricedInPayload>();
        candidates.AddRange(StaticItems());
        candidates.AddRange(DerivedItems());

        var fresh = candidates.Where(c => !used.Contains(c.Prompt)).ToList();
        if (fresh.Count == 0)
        {
            _logger.LogInformation("Priced In: every magnitude has been used already ({Total} in bank)",
                candidates.Count);
            return null;
        }

        // Deterministic pick so regenerating the same day yields the same puzzle.
        var pick = fresh[new Random(date.DayNumber).Next(fresh.Count)];

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(pick),
            GenerationSource = CivicGenerationSource.Seed,
        };
    }

    private List<PricedInPayload> StaticItems()
    {
        var bank = SeedService.LoadJson<List<MagnitudeSeedItem>>("Seed.magnitudes.json") ?? new();
        var items = new List<PricedInPayload>();

        foreach (var m in bank)
        {
            if (!m.Verified)
            {
                _logger.LogWarning("Priced In: skipping unverified magnitude {Key}", m.Key);
                continue;
            }

            items.Add(new PricedInPayload(
                m.Prompt, m.Unit, m.MinBound, m.MaxBound, MaxGuesses,
                m.TrueValue, m.Anchor, m.Source, m.SourceUrl, m.AsOf));
        }

        return items;
    }

    /// <summary>
    /// Figures computed from the tax model rather than authored — real, current, and
    /// impossible to leave stale. Uses a fixed set of (income, state) pairs so the bank
    /// is stable across runs.
    /// </summary>
    private static List<PricedInPayload> DerivedItems()
    {
        var items = new List<PricedInPayload>();
        var incomes = new[] { 45_000d, 60_000d, 90_000d, 150_000d };

        foreach (var income in incomes)
        {
            var federal = TaxEngine.ComputeFederal(income, FilingStatus.Single);
            items.Add(new PricedInPayload(
                Prompt: $"What did a single filer earning ${income:N0} owe in total federal tax " +
                        $"(income tax plus payroll) in {TaxConstants.TaxYear}?",
                Unit: "usd",
                MinBound: 100,
                MaxBound: 500_000,
                MaxGuesses: MaxGuesses,
                TrueValue: Math.Round(federal.Total),
                Anchor: $"That's an effective rate of {federal.EffectiveRate:P1} — payroll tax alone accounts for " +
                        $"${Math.Round(federal.Fica):N0} of it.",
                Source: $"Civersify tax model, {TaxConstants.TaxYear} federal constants",
                SourceUrl: null,
                AsOf: $"{TaxConstants.TaxYear}-01-01"));
        }

        // One state comparison — the "where you live changes the bill" beat.
        foreach (var code in new[] { "CA", "TX" })
        {
            var profile = StateProfiles.Find(code);
            if (profile is null) continue;

            const double income = 90_000d;
            var state = TaxEngine.ComputeState(income, profile);
            items.Add(new PricedInPayload(
                Prompt: $"What did a single filer earning ${income:N0} in {profile.Name} owe in state and local tax " +
                        $"(income, sales, and property combined)?",
                Unit: "usd",
                MinBound: 50,
                MaxBound: 200_000,
                MaxGuesses: MaxGuesses,
                TrueValue: Math.Round(state.Total),
                Anchor: $"An effective state and local rate of {state.EffectiveRate:P1}. " +
                        $"{profile.Notes}",
                Source: "Civersify tax model, state profile parameters",
                SourceUrl: null,
                AsOf: $"{TaxConstants.TaxYear}-01-01"));
        }

        return items;
    }

    private async Task<HashSet<string>> UsedPromptsAsync(CancellationToken ct)
    {
        var payloads = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.PricedIn)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var used = new HashSet<string>();
        foreach (var json in payloads)
        {
            try { used.Add(DailyJson.Deserialize<PricedInPayload>(json).Prompt); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Priced In: skipping unreadable historical payload");
            }
        }
        return used;
    }

    private sealed class MagnitudeSeedItem
    {
        public string Key { get; set; } = "";
        public string Prompt { get; set; } = "";
        public double TrueValue { get; set; }
        public string Unit { get; set; } = "usd";
        public double MinBound { get; set; }
        public double MaxBound { get; set; }
        public string Anchor { get; set; } = "";
        public string Source { get; set; } = "";
        public string? SourceUrl { get; set; }
        public string? AsOf { get; set; }

        /// <summary>"smaller" | "bigger" — audit tag for the bank-balance check.</summary>
        public string Direction { get; set; } = "";

        public bool Verified { get; set; }
    }
}
