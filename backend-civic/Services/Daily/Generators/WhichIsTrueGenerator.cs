using System.Text.Json.Nodes;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Civic.API.Services.TaxModel;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Which Is True (07): a question and two figures, one of which answers it.
///
/// <b>The invariant.</b> Both options are REAL. The decoy is never a fabricated number —
/// it is always another true figure from the same family: a different state's sales tax,
/// a different filing status's deduction, a different bill's sponsor. Three reasons this
/// is worth the extra work over "multiply the answer by 2.4":
///
///  1. A made-up figure is a made-up figure even when it's labelled wrong, and this app's
///     whole credibility rests on never showing one (see <see cref="PricedInGenerator"/>).
///  2. Invented decoys are guessable. Players learn within a week that the rounder or the
///     more extreme number is the fake, and the game stops measuring anything.
///  3. The reveal gets to teach twice — "that's the right number, and the other one is
///     Ohio's" is a better beat than "the other one was nothing."
///
/// Sources, in preference order, all already in the repo and all zero-LLM:
///   - <b>State &amp; local tax</b> — <see cref="StateProfiles"/>, 50 states of verified
///     Tax Foundation 2025 sales and property rates. The deepest pool.
///   - <b>Federal budget</b> — Seed/magnitudes.json (verified rows only) and the
///     <see cref="TaxConstants"/> bracket/deduction table.
///   - <b>Congress</b> — ingested <see cref="Bill"/> rows: who sponsored it, which chamber
///     it started in, what year it was introduced.
/// </summary>
public class WhichIsTrueGenerator : IDailyPuzzleGenerator
{
    public const int RoundsPerPuzzle = 5;

    /// <summary>Below this, the day is skipped rather than shipping a two-round puzzle.</summary>
    public const int MinRounds = 4;

    /// <summary>
    /// No more than this many rounds from one topic, so a puzzle is never five sales-tax
    /// questions in a row.
    /// </summary>
    public const int MaxPerTopic = 2;

    /// <summary>
    /// At most ONE round per question family (the key prefix — "state-sales", "bracket",
    /// "bill-year"). The topic cap alone isn't enough: two bracket questions are both
    /// "Federal budget" and read as the same question asked twice, which is the fastest
    /// way to make a five-round puzzle feel like a two-round one.
    /// </summary>
    public const int MaxPerFamily = 1;

    /// <summary>
    /// Rival-gap band for sales rates, in percentage points. The floor keeps a round from
    /// being a coin flip on rounding; the CEILING is the less obvious half — pairing every
    /// state against the national extreme would make "never pick the 0.00%" a winning
    /// strategy and would show the same two rivals all week.
    /// </summary>
    public const double MinSalesGapPoints = 1.5;
    public const double MaxSalesGapPoints = 4.0;

    /// <summary>Same band for property rates, which live on a much narrower scale.</summary>
    public const double MinPropertyGapPoints = 0.6;
    public const double MaxPropertyGapPoints = 1.4;

    /// <summary>
    /// Ratio band for dollar figures, the magnitude analogue of the rate band above. The
    /// ceiling matters just as much: nobody believes the standard deduction is $626,350, so
    /// a 40x pairing is a free point rather than a question.
    /// </summary>
    public const double MinMagnitudeRatio = 1.8;
    public const double MaxMagnitudeRatio = 8.0;

    private readonly CivicDbContext _db;
    private readonly ILogger<WhichIsTrueGenerator> _logger;

    public WhichIsTrueGenerator(CivicDbContext db, ILogger<WhichIsTrueGenerator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.WhichIsTrue;

    /// <summary>Pure selection over verified rows — nothing here can read as an opinion.</summary>
    public bool RequiresReview => false;

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var used = await UsedKeysAsync(ct);
        var rng = new Random(date.DayNumber);

        // Candidates per topic, each already shuffled, so the pick below is just "take the
        // next unused one" while the per-topic cap keeps the mix honest.
        var byTopic = new Dictionary<string, List<Candidate>>
        {
            [WhichIsTrueTopic.StateAndLocalTax] = Shuffled(StateTaxCandidates(), rng),
            [WhichIsTrueTopic.FederalBudget] = Shuffled(FederalCandidates(), rng),
            [WhichIsTrueTopic.Congress] = Shuffled(await BillCandidatesAsync(ct), rng),
        };

        var rounds = new List<WhichIsTrueRound>();
        var perTopic = byTopic.Keys.ToDictionary(t => t, _ => 0);
        var perFamily = new Dictionary<string, int>();

        // Round-robin across topics so a thin pool doesn't push the whole puzzle onto the
        // deepest one. Stops when the slate is full or nothing eligible is left anywhere.
        var topics = byTopic.Keys.ToList();
        var progressed = true;
        while (rounds.Count < RoundsPerPuzzle && progressed)
        {
            progressed = false;
            foreach (var topic in topics)
            {
                if (rounds.Count >= RoundsPerPuzzle) break;
                if (perTopic[topic] >= MaxPerTopic) continue;

                var next = byTopic[topic].FirstOrDefault(c =>
                    !used.Contains(c.Key)
                    && perFamily.GetValueOrDefault(Family(c.Key)) < MaxPerFamily);
                if (next is null) continue;

                byTopic[topic].Remove(next);
                used.Add(next.Key);
                perTopic[topic]++;
                perFamily[Family(next.Key)] = perFamily.GetValueOrDefault(Family(next.Key)) + 1;
                rounds.Add(next.ToRound(date));
                progressed = true;
            }
        }

        if (rounds.Count < MinRounds)
        {
            _logger.LogInformation(
                "Which Is True: only {Found}/{Needed} unused rounds for {Date} — skipping the day",
                rounds.Count, MinRounds, date);
            return null;
        }

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(new WhichIsTruePayload(rounds)),
            // Bill rounds are cut from ingested content; the tax rows are seeded. "Seed" is
            // the honest label for the majority and matches what the admin page expects.
            GenerationSource = CivicGenerationSource.Seed,
            SourceBillId = rounds.Select(r => r.BillId).FirstOrDefault(id => id is not null),
        };
    }

    // ------------------------------------------------------------- Candidates

    /// <summary>
    /// A question with its true answer and a real rival, before the A/B coin flip. Kept
    /// separate from <see cref="WhichIsTrueRound"/> so the side assignment happens in
    /// exactly one place (<see cref="ToRound"/>) and can be tested on its own.
    /// </summary>
    public sealed record Candidate(
        string Key,
        string Topic,
        string Prompt,
        string TruthText,
        string DecoyText,
        string Explanation,
        string DecoyTruth,
        string Source,
        string? SourceUrl,
        string? AsOf,
        Guid? BillId)
    {
        /// <summary>
        /// Assign the truth to A or B. Seeded off the round key and the date rather than a
        /// shared RNG, so which side is right is stable when a day is regenerated and does
        /// not depend on the order the candidates happened to be built in. (A shared
        /// sequence would also correlate side with position — every first round landing on
        /// A on the same days.)
        /// </summary>
        public WhichIsTrueRound ToRound(DateOnly date)
        {
            var truthIsA = StableHash($"{date.DayNumber}:{Key}") % 2 == 0;
            return new WhichIsTrueRound(
                Key, Topic, Prompt,
                OptionA: truthIsA ? TruthText : DecoyText,
                OptionB: truthIsA ? DecoyText : TruthText,
                Correct: truthIsA ? "A" : "B",
                Explanation, DecoyTruth, Source, SourceUrl, AsOf, BillId);
        }
    }

    /// <summary>
    /// Sales and property rates for all 50 states, each paired against another state's real
    /// rate. The pairing is what makes the reveal land: "Tennessee really is 9.55% — the
    /// other number is Maine's."
    /// </summary>
    public static List<Candidate> StateTaxCandidates()
    {
        const string source = "Tax Foundation, 2025 (via Civersify state tax profiles)";
        var states = StateProfiles.All.ToList();
        var candidates = new List<Candidate>();

        foreach (var state in states)
        {
            var salesRival = RivalFor(states, state, s => s.SalesRate,
                MinSalesGapPoints, MaxSalesGapPoints, $"state-sales:{state.Code}");
            if (salesRival is not null)
            {
                candidates.Add(new Candidate(
                    Key: $"state-sales:{state.Code}",
                    Topic: WhichIsTrueTopic.StateAndLocalTax,
                    Prompt: $"What is the average combined state and local sales tax rate in {state.Name}?",
                    TruthText: Percent(state.SalesRate),
                    DecoyText: Percent(salesRival.SalesRate),
                    Explanation: $"{state.Name} averages {Percent(state.SalesRate)} once local add-ons are " +
                                 "included — the rate you actually pay at the register, not the state rate alone.",
                    DecoyTruth: $"{Percent(salesRival.SalesRate)} is {salesRival.Name}'s.",
                    Source: source,
                    SourceUrl: "https://taxfoundation.org/data/all/state/2025-sales-taxes/",
                    AsOf: "2025-07-01",
                    BillId: null));
            }

            var propertyRival = RivalFor(states, state, s => s.PropRate,
                MinPropertyGapPoints, MaxPropertyGapPoints, $"state-property:{state.Code}");
            if (propertyRival is not null)
            {
                candidates.Add(new Candidate(
                    Key: $"state-property:{state.Code}",
                    Topic: WhichIsTrueTopic.StateAndLocalTax,
                    Prompt: $"What is the effective property tax rate on owner-occupied housing in {state.Name}?",
                    TruthText: Percent(state.PropRate),
                    DecoyText: Percent(propertyRival.PropRate),
                    // Kept to what's true of THIS row: a generic "no-income-tax states sit
                    // high" line rendered under Mississippi's 0.55%, which is both a
                    // low rate and an income-tax state. The dollar figure always holds.
                    Explanation: $"{state.Name} works out to {Percent(state.PropRate)} of a home's value per " +
                                 $"year — about {Money(state.PropRate * ExampleHomeValue)} on a " +
                                 $"{Money(ExampleHomeValue)} home.",
                    DecoyTruth: $"{Percent(propertyRival.PropRate)} is {propertyRival.Name}'s.",
                    Source: source,
                    SourceUrl: "https://taxfoundation.org/data/all/state/property-taxes-by-state-county/",
                    AsOf: "2025-01-01",
                    BillId: null));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Federal figures: the verified magnitude bank paired against each other, plus the
    /// bracket thresholds, which are already a family of real numbers that look alike.
    /// </summary>
    public static List<Candidate> FederalCandidates()
    {
        var candidates = new List<Candidate>();
        candidates.AddRange(MagnitudeCandidates());
        candidates.AddRange(BracketCandidates());
        return candidates;
    }

    private static List<Candidate> MagnitudeCandidates()
    {
        var bank = (SeedService.LoadJson<List<JsonObject>>("Seed.magnitudes.json") ?? new())
            .Where(i => i["verified"]?.GetValue<bool>() == true)
            .Select(i => new
            {
                Key = i["key"]!.GetValue<string>(),
                Prompt = i["prompt"]!.GetValue<string>(),
                Value = i["trueValue"]!.GetValue<double>(),
                Unit = i["unit"]?.GetValue<string>() ?? "usd",
                Anchor = i["anchor"]?.GetValue<string>() ?? "",
                Source = i["source"]?.GetValue<string>() ?? "",
                SourceUrl = i["sourceUrl"]?.GetValue<string>(),
                AsOf = i["asOf"]?.GetValue<string>(),
            })
            .ToList();

        var candidates = new List<Candidate>();
        foreach (var item in bank)
        {
            // Any same-unit figure inside the ratio band, picked deterministically — far
            // enough that the reveal teaches a second real number, close enough that
            // telling them apart is knowledge rather than arithmetic.
            var rivals = bank
                .Where(o => o.Key != item.Key && o.Unit == item.Unit && o.Value > 0)
                .Where(o => InMagnitudeBand(item.Value, o.Value))
                .OrderBy(o => o.Key, StringComparer.Ordinal)
                .ToList();
            if (rivals.Count == 0) continue;
            var rival = rivals[(int)(StableHash($"magnitude:{item.Key}") % (uint)rivals.Count)];

            candidates.Add(new Candidate(
                Key: $"magnitude:{item.Key}",
                Topic: WhichIsTrueTopic.FederalBudget,
                Prompt: item.Prompt,
                TruthText: Money(item.Value),
                DecoyText: Money(rival.Value),
                Explanation: item.Anchor,
                DecoyTruth: $"{Money(rival.Value)} is real too — it's the answer to: {rival.Prompt}",
                Source: item.Source,
                SourceUrl: item.SourceUrl,
                AsOf: item.AsOf,
                BillId: null));
        }

        return candidates;
    }

    /// <summary>
    /// "Where does the 24% bracket start?" against the real start of another bracket. Every
    /// number on the card is a live IRS threshold, which is exactly why it's hard.
    /// </summary>
    private static List<Candidate> BracketCandidates()
    {
        var candidates = new List<Candidate>();

        foreach (var filing in new[] { FilingStatus.Single, FilingStatus.MarriedFilingJointly })
        {
            var brackets = TaxConstants.Brackets(filing).Where(b => b.Lower > 0).ToList();
            var label = filing == FilingStatus.Single ? "a single filer" : "a married couple filing jointly";

            foreach (var bracket in brackets)
            {
                var rivals = brackets
                    .Where(b => InMagnitudeBand(bracket.Lower, b.Lower))
                    .OrderBy(b => b.Lower)
                    .ToList();
                if (rivals.Count == 0) continue;
                var key = $"bracket:{filing}:{bracket.Rate:0.000}";
                var rival = rivals[(int)(StableHash(key) % (uint)rivals.Count)];

                candidates.Add(new Candidate(
                    Key: key,
                    Topic: WhichIsTrueTopic.FederalBudget,
                    Prompt: $"At what taxable income does the {Rate(bracket.Rate)} federal bracket begin for " +
                            $"{label} in {TaxConstants.TaxYear}?",
                    TruthText: Money(bracket.Lower),
                    DecoyText: Money(rival.Lower),
                    Explanation: $"Only income above {Money(bracket.Lower)} is taxed at {Rate(bracket.Rate)} — " +
                                 "every dollar below it is still taxed at the lower marginal rates.",
                    DecoyTruth: $"{Money(rival.Lower)} is where the {Rate(rival.Rate)} bracket begins.",
                    Source: $"IRS Rev. Proc. 2024-40 ({TaxConstants.TaxYear} brackets)",
                    SourceUrl: "https://www.irs.gov/pub/irs-drop/rp-24-40.pdf",
                    AsOf: $"{TaxConstants.TaxYear}-01-01",
                    BillId: null));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Facts about legislation actually before Congress. Every decoy is another ingested
    /// bill's real sponsor / status / year, so a player who knows the landscape is rewarded
    /// and nobody is shown an invented member of Congress.
    /// </summary>
    public async Task<List<Candidate>> BillCandidatesAsync(CancellationToken ct)
    {
        var bills = await _db.Bills
            .Where(b => b.SynthesisStatus == BillSynthesisStatus.Synthesized)
            .OrderByDescending(b => b.LatestActionDate ?? b.IntroducedDate)
            .Take(200)
            .ToListAsync(ct);

        var candidates = new List<Candidate>();

        foreach (var bill in bills)
        {
            var name = bill.ShortTitle ?? bill.Title;
            var url = bill.SourceUrl ?? bill.FullTextUrl;
            var citation = $"Congress.gov · {Cite(bill)}";

            // Sponsor — decoy is another current bill's real sponsor.
            var sponsorRival = bills.FirstOrDefault(o =>
                o.Id != bill.Id
                && !string.IsNullOrWhiteSpace(o.Sponsor)
                && !string.Equals(o.Sponsor, bill.Sponsor, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(bill.Sponsor) && sponsorRival is not null)
            {
                candidates.Add(new Candidate(
                    Key: $"bill-sponsor:{bill.Id}",
                    Topic: WhichIsTrueTopic.Congress,
                    Prompt: $"Who introduced {name}?",
                    TruthText: bill.Sponsor,
                    DecoyText: sponsorRival.Sponsor,
                    Explanation: $"{bill.Sponsor} introduced {Cite(bill)} on " +
                                 $"{bill.IntroducedDate:MMMM d, yyyy}.",
                    DecoyTruth: $"{sponsorRival.Sponsor} sponsored {Cite(sponsorRival)} instead.",
                    Source: citation,
                    SourceUrl: url,
                    AsOf: bill.IntroducedDate.ToString("yyyy-MM-dd"),
                    BillId: bill.Id));
            }

            // Chamber — both options are real chambers; one of them is where it started.
            var chamber = Chamber(bill.BillType);
            if (chamber is not null)
            {
                candidates.Add(new Candidate(
                    Key: $"bill-chamber:{bill.Id}",
                    Topic: WhichIsTrueTopic.Congress,
                    Prompt: $"Which chamber was {name} introduced in?",
                    TruthText: chamber,
                    DecoyText: chamber == House ? Senate : House,
                    Explanation: $"{Cite(bill)} — the \"{bill.BillType.ToUpperInvariant()}\" prefix is " +
                                 $"what tells you: it started in {chamber}.",
                    DecoyTruth: $"A bill starting in {(chamber == House ? Senate : House)} would be numbered " +
                                $"differently.",
                    Source: citation,
                    SourceUrl: url,
                    AsOf: bill.IntroducedDate.ToString("yyyy-MM-dd"),
                    BillId: bill.Id));
            }

            // Year introduced — decoy is another bill's real introduction year.
            var yearRival = bills.FirstOrDefault(o =>
                o.Id != bill.Id && o.IntroducedDate.Year != bill.IntroducedDate.Year);
            if (yearRival is not null)
            {
                candidates.Add(new Candidate(
                    Key: $"bill-year:{bill.Id}",
                    Topic: WhichIsTrueTopic.Congress,
                    Prompt: $"In what year was {name} introduced?",
                    TruthText: bill.IntroducedDate.Year.ToString(),
                    DecoyText: yearRival.IntroducedDate.Year.ToString(),
                    Explanation: $"{Cite(bill)} was introduced on {bill.IntroducedDate:MMMM d, yyyy}.",
                    DecoyTruth: $"{yearRival.IntroducedDate.Year} is when {Cite(yearRival)} was introduced.",
                    Source: citation,
                    SourceUrl: url,
                    AsOf: bill.IntroducedDate.ToString("yyyy-MM-dd"),
                    BillId: bill.Id));
            }
        }

        return candidates;
    }

    // --------------------------------------------------------------- Plumbing

    /// <summary>Reference home value for the property-tax explanation. Round on purpose — it's an illustration, not a claim about local prices.</summary>
    private const double ExampleHomeValue = 300_000;

    private const string House = "The House of Representatives";
    private const string Senate = "The Senate";

    /// <summary>Chamber of origin from the upstream bill-type code, or null if unrecognized.</summary>
    public static string? Chamber(string billType) => billType?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        var t when t.StartsWith("HR") || t.StartsWith("HJRES") || t.StartsWith("HCONRES")
                || t.StartsWith("HRES") || t == "H" => House,
        var t when t.StartsWith("SJRES") || t.StartsWith("SCONRES") || t.StartsWith("SRES")
                || t.StartsWith("S") => Senate,
        _ => null,
    };

    private async Task<HashSet<string>> UsedKeysAsync(CancellationToken ct)
    {
        var payloads = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.WhichIsTrue)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var used = new HashSet<string>();
        foreach (var json in payloads)
        {
            try
            {
                foreach (var round in DailyJson.Deserialize<WhichIsTruePayload>(json).Rounds)
                    used.Add(round.Key);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                // A payload from an older shape shouldn't stop today's generation.
                _logger.LogWarning(ex, "Which Is True: skipping unreadable historical payload");
            }
        }
        return used;
    }

    /// <summary>
    /// A rival state whose rate sits a meaningful — but not extreme — distance from this
    /// one's, picked deterministically from every state in the band so the bank shows real
    /// variety rather than the same two outliers over and over. Null when nothing qualifies.
    /// </summary>
    private static StateProfile? RivalFor(
        List<StateProfile> states, StateProfile from, Func<StateProfile, double> measure,
        double minGap, double maxGap, string key)
    {
        var eligible = states
            .Where(s => s.Code != from.Code)
            .Where(s =>
            {
                var gap = Gap(measure(s), measure(from));
                return gap >= minGap && gap <= maxGap;
            })
            .OrderBy(s => s.Code, StringComparer.Ordinal)   // stable order before the pick
            .ToList();

        return eligible.Count == 0 ? null : eligible[(int)(StableHash(key) % (uint)eligible.Count)];
    }

    /// <summary>The question family a round key belongs to — everything before the colon.</summary>
    public static string Family(string key)
    {
        var colon = key.IndexOf(':');
        return colon < 0 ? key : key[..colon];
    }

    private static double Gap(double a, double b) => Math.Abs(a - b) * 100;

    /// <summary>Two positive figures far enough apart to be a question, close enough to be a hard one.</summary>
    private static bool InMagnitudeBand(double a, double b)
    {
        if (a <= 0 || b <= 0) return false;
        var ratio = a >= b ? a / b : b / a;
        return ratio >= MinMagnitudeRatio && ratio <= MaxMagnitudeRatio;
    }

    private static string Percent(double rate) => $"{rate * 100:0.00}%";

    /// <summary>
    /// A bracket rate as "32%". Not the "P0" format — that renders "32 %" under the
    /// invariant culture, which reads as a typo in a headline-sized prompt.
    /// </summary>
    private static string Rate(double rate) => $"{rate * 100:0.#}%";

    private static string Money(double value) => $"${value:N0}";

    private static List<Candidate> Shuffled(List<Candidate> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }

    /// <summary>
    /// FNV-1a. Deliberately not <c>string.GetHashCode</c> or <c>HashCode.Combine</c>, both of
    /// which are randomized per process — a puzzle regenerated after a restart would flip
    /// which side the answer is on.
    /// </summary>
    public static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }

    /// <summary>Display id for prose, falling back to the external id for odd rows.</summary>
    private static string Cite(Bill bill) =>
        string.IsNullOrWhiteSpace(bill.BillType) || bill.Number <= 0
            ? bill.ExternalId
            : BillMappings.Identifier(bill.BillType.ToUpperInvariant(), bill.Number, bill.Congress);
}
