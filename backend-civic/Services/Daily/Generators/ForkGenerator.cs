using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Fork (01): the daily "would you rather", cut from a live coalition provision.
///
/// <see cref="SubQuestion"/> is already the right shape — Prompt, TradeoffDescription and
/// a PositionOptions array — so when a sub-question carries two or more options this is
/// pure selection with no LLM at all. When it doesn't, we fall back to the hand-authored
/// evergreen bank rather than spending a model call.
/// </summary>
public class ForkGenerator : IDailyPuzzleGenerator
{
    private readonly CivicDbContext _db;
    private readonly ICivicCatalog _catalog;
    private readonly ILogger<ForkGenerator> _logger;

    public ForkGenerator(CivicDbContext db, ICivicCatalog catalog, ILogger<ForkGenerator> logger)
    {
        _db = db;
        _catalog = catalog;
        _logger = logger;
    }

    public DailyGameKind Kind => DailyGameKind.Fork;

    /// <summary>
    /// Requires review: a "would you rather" whose options aren't equally costly reads as
    /// partisan, and this is the most publicly shareable of the six.
    /// </summary>
    public bool RequiresReview => true;

    private static readonly ProvisionState[] OpenStates =
    {
        ProvisionState.Open, ProvisionState.Contested, ProvisionState.NearCoalition,
    };

    public async Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct)
    {
        var usedKeys = await UsedKeysAsync(ct);

        var openProvisionIds = await _db.Provisions
            .Where(p => OpenStates.Contains(p.State))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var subQuestions = await _db.SubQuestions
            .Where(s => openProvisionIds.Contains(s.ProvisionId))
            // Birth sub-questions in order are the central cruxes, not long-tail details.
            .OrderBy(s => s.Origin == SubQuestionOrigin.Birth ? 0 : 1)
            .ThenBy(s => s.OrderIndex)
            .ToListAsync(ct);

        foreach (var sq in subQuestions)
        {
            if (usedKeys.Contains((sq.ProvisionId, sq.Key))) continue;
            if (sq.PositionOptions.Length < 2) continue;

            // A sub-question with no tradeoff line can't state what either option costs,
            // so the validator would reject it anyway. Skip quietly rather than logging a
            // rejection for every extracted crux on every pass — today most provisions
            // have no TradeoffDescription, so this is the common case, not an anomaly.
            if (string.IsNullOrWhiteSpace(sq.TradeoffDescription)) continue;

            var provision = await _db.Provisions.FirstOrDefaultAsync(p => p.Id == sq.ProvisionId, ct);
            if (provision is null) continue;

            var payload = new ForkPayload(
                Question: sq.Prompt,
                Tradeoff: sq.TradeoffDescription ?? "",
                OptionA: new ForkOption(sq.PositionOptions[0], CostFor(sq, 0)),
                OptionB: new ForkOption(sq.PositionOptions[1], CostFor(sq, 1)),
                AxisKey: AxisForProvision(provision),
                SubQuestionKey: sq.Key,
                ProvisionSlug: provision.Slug);

            if (!ForkValidator.IsAcceptable(payload, out var reason))
            {
                _logger.LogInformation("Fork: rejected {ProvisionSlug}/{Key} — {Reason}",
                    provision.Slug, sq.Key, reason);
                continue;
            }

            return new DailyPuzzle
            {
                Kind = Kind,
                PuzzleDate = date,
                PayloadJson = DailyJson.Serialize(payload),
                SourceProvisionId = provision.Id,
                GenerationSource = CivicGenerationSource.News,
            };
        }

        return await FromSeedBankAsync(date, usedKeys, ct);
    }

    /// <summary>
    /// The evergreen fallback bank. Used when no live provision has a usable sub-question —
    /// so the slate is never empty and no LLM call is needed to fill it.
    /// </summary>
    private async Task<DailyPuzzle?> FromSeedBankAsync(
        DateOnly date, HashSet<(Guid, string)> usedKeys, CancellationToken ct)
    {
        var bank = SeedService.LoadJson<List<ForkSeedItem>>("Seed.fork-fallback.json") ?? new();
        if (bank.Count == 0)
        {
            _logger.LogWarning("Fork: no eligible sub-question and the fallback bank is empty");
            return null;
        }

        var usedSeedKeys = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.Fork && p.SourceProvisionId == null)
            .Select(p => p.PayloadJson)
            .ToListAsync(ct);

        var seen = new HashSet<string>();
        foreach (var json in usedSeedKeys)
        {
            try { seen.Add(DailyJson.Deserialize<ForkPayload>(json).SubQuestionKey); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException) { }
        }

        var item = bank.FirstOrDefault(i => !seen.Contains(i.Key));
        if (item is null)
        {
            _logger.LogInformation("Fork: fallback bank exhausted ({Count} items all used)", bank.Count);
            return null;
        }

        var payload = new ForkPayload(
            item.Question, item.Tradeoff,
            new ForkOption(item.OptionA.Label, item.OptionA.Cost),
            new ForkOption(item.OptionB.Label, item.OptionB.Cost),
            item.AxisKey, item.Key, ProvisionSlug: null);

        if (!ForkValidator.IsAcceptable(payload, out var reason))
        {
            _logger.LogWarning("Fork: seed item {Key} fails the validator — {Reason}", item.Key, reason);
            return null;
        }

        return new DailyPuzzle
        {
            Kind = Kind,
            PuzzleDate = date,
            PayloadJson = DailyJson.Serialize(payload),
            GenerationSource = CivicGenerationSource.Seed,
        };
    }

    /// <summary>
    /// Each option's stated cost. Sub-questions don't carry per-option costs, so the
    /// tradeoff line stands in — the validator then requires it to be non-empty, which is
    /// what keeps "both options must be costly" enforceable.
    /// </summary>
    private static string CostFor(SubQuestion sq, int index) =>
        string.IsNullOrWhiteSpace(sq.TradeoffDescription)
            ? ""
            : sq.TradeoffDescription;

    /// <summary>
    /// Best-effort axis attribution for the reveal line. Provisions don't carry an axis,
    /// so fall back to the first catalog axis rather than inventing one.
    /// </summary>
    private string AxisForProvision(Provision provision) =>
        _catalog.Axes.FirstOrDefault()?.Key ?? "govt-role";

    private async Task<HashSet<(Guid, string)>> UsedKeysAsync(CancellationToken ct)
    {
        var rows = await _db.DailyPuzzles
            .Where(p => p.Kind == DailyGameKind.Fork && p.SourceProvisionId != null)
            .Select(p => new { p.SourceProvisionId, p.PayloadJson })
            .ToListAsync(ct);

        var used = new HashSet<(Guid, string)>();
        foreach (var row in rows)
        {
            try
            {
                var key = DailyJson.Deserialize<ForkPayload>(row.PayloadJson).SubQuestionKey;
                used.Add((row.SourceProvisionId!.Value, key));
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Fork: skipping unreadable historical payload");
            }
        }
        return used;
    }

    private sealed class ForkSeedItem
    {
        public string Key { get; set; } = "";
        public string Question { get; set; } = "";
        public string Tradeoff { get; set; } = "";
        public ForkSeedOption OptionA { get; set; } = new();
        public ForkSeedOption OptionB { get; set; } = new();
        public string AxisKey { get; set; } = "";
    }

    private sealed class ForkSeedOption
    {
        public string Label { get; set; } = "";
        public string Cost { get; set; } = "";
    }
}

/// <summary>
/// The neutrality gate for Fork. The failure mode for "would you rather" is that one
/// option is an applause line and the other a strawman, so a puzzle only ships when both
/// options state what they COST — and never when an option editorializes.
/// </summary>
public static class ForkValidator
{
    /// <summary>
    /// Words that turn an option into an argument. Party and role names are excluded
    /// because Fork must stay about tradeoffs, never about sides.
    /// </summary>
    private static readonly string[] Disqualifying =
    {
        "obviously", "common sense", "extremist", "radical", "insane",
        "democrat", "republican", "liberal", "conservative", "left-wing", "right-wing",
        "maga", "socialist", "fascist",
    };

    public static bool IsAcceptable(ForkPayload payload, out string reason)
    {
        if (string.IsNullOrWhiteSpace(payload.Question))
        {
            reason = "empty question";
            return false;
        }

        // Rule 1: an option with no stated cost is rejected. This is the whole guard —
        // if we can't say what an option costs you, it isn't a tradeoff.
        if (string.IsNullOrWhiteSpace(payload.OptionA.Cost) || string.IsNullOrWhiteSpace(payload.OptionB.Cost))
        {
            reason = "an option has no stated cost";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.OptionA.Label) || string.IsNullOrWhiteSpace(payload.OptionB.Label))
        {
            reason = "an option has no label";
            return false;
        }

        // Rule 2: no partisan or editorializing language anywhere in the puzzle.
        var haystack = string.Join(" ",
            payload.Question, payload.Tradeoff,
            payload.OptionA.Label, payload.OptionA.Cost,
            payload.OptionB.Label, payload.OptionB.Cost).ToLowerInvariant();

        foreach (var term in Disqualifying)
        {
            if (haystack.Contains(term, StringComparison.Ordinal))
            {
                reason = $"contains disqualifying term \"{term}\"";
                return false;
            }
        }

        reason = "";
        return true;
    }
}
