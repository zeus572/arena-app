using Civic.API.Models.Rooms;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Thrown when code tries to add up money that must not be added up.
///
/// Deliberately an exception rather than a silently-skipped row. The failure this guards
/// against — quietly summing a request with an appropriation, or an outlay with a modelled
/// economic effect — produces a plausible-looking number that is simply wrong, and a wrong
/// number nobody notices is the exact harm the Money Trail exists to prevent.
/// </summary>
public class MoneySumException : Exception
{
    public MoneySumException(string message) : base(message) { }
}

/// <summary>
/// Arithmetic on money items, with the rules that make the arithmetic honest (PRD 05 §9).
///
/// Pure. Every rule here has a test, because these are the invariants the whole feature is
/// for — the product's claim is that it will not conflate a request with a payment, and
/// that claim is only as good as this file.
/// </summary>
public static class MoneyMath
{
    /// <summary>The ladder, in order. Index is the rung number.</summary>
    public static readonly IReadOnlyList<FundingStage> Ladder = new[]
    {
        FundingStage.Requested,
        FundingStage.Authorized,
        FundingStage.Appropriated,
        FundingStage.Obligated,
        FundingStage.Spent,
    };

    /// <summary>
    /// Build the full five-rung ladder for a new item, including the empty rungs.
    ///
    /// Callers do not get to write only the rungs they have data for. Rendering an item
    /// with three rows instead of five would hide that two stages are empty, which is
    /// usually the most important thing on the page.
    /// </summary>
    public static List<MoneyStageEntry> BuildLadder(
        Guid moneyItemId,
        IReadOnlyDictionary<FundingStage, decimal> known,
        IReadOnlyDictionary<FundingStage, string>? notApplicable = null)
    {
        var rows = new List<MoneyStageEntry>();

        foreach (var stage in Ladder)
        {
            var hasAmount = known.TryGetValue(stage, out var amount);
            var na = notApplicable is not null && notApplicable.TryGetValue(stage, out var reason)
                ? reason
                : null;

            rows.Add(new MoneyStageEntry
            {
                Id = Guid.NewGuid(),
                MoneyItemId = moneyItemId,
                Stage = stage,
                AmountUsd = hasAmount ? amount : null,
                Applicability = hasAmount
                    ? StageApplicability.Present
                    : na is not null
                        ? StageApplicability.NotApplicable
                        : StageApplicability.EmptyPending,
                NotApplicableReason = hasAmount ? null : na,
            });
        }

        return rows;
    }

    /// <summary>The highest rung actually reached. Requested when nothing has been.</summary>
    public static FundingStage CurrentStage(IEnumerable<MoneyStageEntry> entries)
    {
        var reached = entries
            .Where(e => e.Applicability == StageApplicability.Present)
            .Select(e => e.Stage)
            .ToList();

        return reached.Count == 0
            ? FundingStage.Requested
            : reached.OrderByDescending(s => Ladder.ToList().IndexOf(s)).First();
    }

    /// <summary>
    /// Sum a set of money items.
    ///
    /// Refuses two things outright:
    ///  - mixing <see cref="MoneyItemKind"/>s, because an outlay and a modelled economic
    ///    effect are not the same kind of quantity and adding them produces nonsense;
    ///  - summing anything that is not a <see cref="MoneyItemKind.GovernmentOutlay"/>,
    ///    because estimates and model outputs carry ranges and assumptions that a single
    ///    total silently discards.
    /// </summary>
    public static decimal TotalOutlays(IReadOnlyCollection<MoneyItem> items)
    {
        if (items.Count == 0) return 0m;

        var kinds = items.Select(i => i.Kind).Distinct().ToList();
        if (kinds.Count > 1)
        {
            throw new MoneySumException(
                "Refusing to sum across money kinds (" + string.Join(", ", kinds) + "). "
              + "Government outlays and modelled economic effects are different quantities "
              + "and belong in separate halves of the page.");
        }

        if (kinds[0] != MoneyItemKind.GovernmentOutlay)
        {
            throw new MoneySumException(
                $"Refusing to total {kinds[0]} items. Estimates and modelled effects carry "
              + "ranges and assumptions that a single figure discards.");
        }

        return items.Sum(i => i.AmountUsd ?? 0m);
    }

    /// <summary>
    /// Sum the amounts at ONE stage across items.
    ///
    /// There is deliberately no function that sums across stages, and this one takes the
    /// stage as a required argument so a caller cannot drift into "just add up the ladder".
    /// Requested plus Appropriated is not a quantity — it double-counts the same dollars at
    /// two points in their life.
    /// </summary>
    public static decimal TotalAtStage(
        IReadOnlyCollection<MoneyStageEntry> entries, FundingStage stage)
        => entries
            .Where(e => e.Stage == stage && e.Applicability == StageApplicability.Present)
            .Sum(e => e.AmountUsd ?? 0m);

    /// <summary>
    /// Always throws. Exists so the mistake has a name and a place to be caught.
    ///
    /// Anyone reaching for "the total across the ladder" is about to double-count: the same
    /// dollars appear at Requested, then Appropriated, then Obligated, then Spent. This is
    /// the single most common error in budget coverage, and the one the ladder exists to
    /// make visible.
    /// </summary>
    public static decimal TotalAcrossStages(IReadOnlyCollection<MoneyStageEntry> entries)
        => throw new MoneySumException(
            "There is no total across funding stages. The same dollars appear at Requested, "
          + "Appropriated, Obligated and Spent as they move; adding the rungs together "
          + "double-counts them. Use TotalAtStage with the stage you mean.");

    /// <summary>True when the figure covers more than one fiscal year.</summary>
    public static bool IsMultiYear(MoneyItem item) => item.FiscalYearEnd > item.FiscalYearStart;

    /// <summary>
    /// The period label that must accompany every amount.
    ///
    /// A ten-year score displayed without its period reads as an annual figure, which
    /// overstates it by an order of magnitude. PRD 05 §9 forbids that specifically, so the
    /// label is computed here rather than left to each call site.
    /// </summary>
    public static string PeriodLabel(MoneyItem item)
    {
        var years = item.FiscalYearEnd - item.FiscalYearStart + 1;

        if (years <= 1) return $"FY{item.FiscalYearStart}";
        return $"FY{item.FiscalYearStart}–FY{item.FiscalYearEnd} ({years} years total, not per year)";
    }

    /// <summary>
    /// Annualising a multi-year total is refused rather than approximated.
    ///
    /// Dividing by the number of years assumes an even spread that appropriations never
    /// have, and the result would be presented as a fact.
    /// </summary>
    public static decimal AnnualAmount(MoneyItem item)
    {
        if (IsMultiYear(item))
        {
            throw new MoneySumException(
                $"'{item.Title}' covers FY{item.FiscalYearStart}-FY{item.FiscalYearEnd}. "
              + "A multi-year total cannot be rendered as an annual amount: dividing by the "
              + "number of years assumes an even spread that appropriations do not have.");
        }

        return item.AmountUsd ?? 0m;
    }

    /// <summary>
    /// Whether an amount may be described with a spending verb.
    ///
    /// Only true at Spent. This is the check behind the room's whole thesis: coverage
    /// saying the government "is spending" a requested figure describes the first rung as
    /// if it were the last.
    /// </summary>
    public static bool CanSaySpent(FundingStage stage) => stage == FundingStage.Spent;

    /// <summary>The plain-language verb that is actually true at a stage.</summary>
    public static string VerbFor(FundingStage stage) => stage switch
    {
        FundingStage.Requested => "has been requested",
        FundingStage.Authorized => "has been authorized",
        FundingStage.Appropriated => "has been appropriated",
        FundingStage.Obligated => "has been committed",
        FundingStage.Spent => "has been spent",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown funding stage."),
    };
}
