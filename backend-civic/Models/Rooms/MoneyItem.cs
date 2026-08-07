using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// The five-rung funding ladder (PRD 05 §4, designs 1s and 1t).
///
/// EXACTLY five. PRD 05 also lists "Allocated", "Estimated" and "Economic effect", but
/// those are not rungs: Allocated folds into Appropriated with a note, and the other two
/// are <see cref="MoneyItemKind"/> values. Keeping them out of this enum is what makes
/// "outlays and modelled economic effects are never summed" mechanically true rather than
/// a rule someone has to remember.
/// </summary>
public enum FundingStage
{
    /// <summary>Someone asked. Almost every headline number is here.</summary>
    Requested,
    /// <summary>A program may exist and may receive up to this much. Still not money.</summary>
    Authorized,
    /// <summary>Legal authority to obligate exists.</summary>
    Appropriated,
    /// <summary>Committed — a contract signed, an award made.</summary>
    Obligated,
    /// <summary>Cash has left the Treasury. The only stage that is, in plain language, spent.</summary>
    Spent,
}

/// <summary>
/// What KIND of number this is. The separation that keeps government outlays and modelled
/// economic effects in different halves of the page, and out of the same total.
/// </summary>
public enum MoneyItemKind
{
    /// <summary>Real government money moving through the five stages.</summary>
    GovernmentOutlay,
    /// <summary>A model's output about the wider economy. Never summed with an outlay.</summary>
    ModeledEconomicEffect,
    /// <summary>A projection of future government money. Labelled, never asserted.</summary>
    Estimate,
}

/// <summary>Whether a rung is filled, empty, or does not apply here.</summary>
public enum StageApplicability
{
    /// <summary>An amount is known for this stage.</summary>
    Present,
    /// <summary>Legitimately empty — the money has not got here yet.</summary>
    EmptyPending,
    /// <summary>This stage does not apply to this kind of item, and says why.</summary>
    NotApplicable,
}

public enum DollarBasis
{
    /// <summary>As stated at the time, not inflation adjusted.</summary>
    Nominal,
    /// <summary>Inflation adjusted; <see cref="MoneyItem.RealBaseYear"/> says to when.</summary>
    Real,
}

/// <summary>
/// One funding item, rendered across all five stages including the empty ones.
///
/// The Money Trail exists because coverage routinely describes a REQUEST using verbs that
/// belong to an OUTLAY. Every field here is in service of not doing that.
/// </summary>
public class MoneyItem
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    /// <summary>Nullable so one funding item can appear in several rooms.</summary>
    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = "";

    [MaxLength(60)]
    public string Jurisdiction { get; set; } = "Federal";

    /// <summary>The program's own name, preserved alongside our categorisation.</summary>
    [MaxLength(200)]
    public string? SourceProgramName { get; set; }

    [MaxLength(60)]
    public string? CategoryKey { get; set; }

    public MoneyItemKind Kind { get; set; } = MoneyItemKind.GovernmentOutlay;

    /// <summary>Highest stage with a present amount. Derived, but persisted for indexing.</summary>
    public FundingStage CurrentStage { get; set; } = FundingStage.Requested;

    public decimal? AmountUsd { get; set; }

    /// <summary>
    /// When the source gives a range, both ends are stored and both are shown. Collapsing a
    /// range into a point value invents precision the source did not have.
    /// </summary>
    public decimal? AmountMinUsd { get; set; }
    public decimal? AmountMaxUsd { get; set; }

    public DollarBasis DollarBasis { get; set; } = DollarBasis.Nominal;
    public int? RealBaseYear { get; set; }

    public int FiscalYearStart { get; set; }

    /// <summary>
    /// Equal to <see cref="FiscalYearStart"/> for a single-year item. A multi-year total
    /// must never be rendered as an annual amount, and this is what lets the code tell.
    /// </summary>
    public int FiscalYearEnd { get; set; }

    public bool IsRecurring { get; set; }
    public bool IsNet { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>
    /// REQUIRED. Design 1s puts this in an inverse panel, not a tooltip, because the four
    /// things a number is not are as important as the number. A money item that cannot say
    /// what it does not mean has not been understood well enough to publish.
    /// </summary>
    [Required, MaxLength(1000)]
    public string WhatThisDoesNotMean { get; set; } = "";

    /// <summary>Who acts next, and on what.</summary>
    [MaxLength(300)]
    public string? DecidesNext { get; set; }

    [MaxLength(500)]
    public string? EstimateMethod { get; set; }

    public double Confidence { get; set; } = 1.0;

    /// <summary>What is knowingly left out of the figure.</summary>
    public string[] Exclusions { get; set; } = Array.Empty<string>();

    public List<MoneyBreakdownLine> Breakdown { get; set; } = new();

    /// <summary>
    /// Comparisons, INCLUDING the rejected ones.
    ///
    /// Design 1s deliberately shows a struck-through per-capita comparison with the reason
    /// it was rejected. That is content, not a code comment — the reader learns more from
    /// seeing a bad comparison refused than from never seeing it.
    /// </summary>
    public List<MoneyComparison> Comparisons { get; set; } = new();

    public DateTime? LastReviewedAt { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public List<FieldProvenance> Provenance { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MoneyBreakdownLine
{
    [Required, MaxLength(200)]
    public string Label { get; set; } = "";

    public decimal AmountUsd { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}

/// <summary>A comparison offered to the reader, or explicitly refused.</summary>
public class MoneyComparison
{
    [Required, MaxLength(300)]
    public string Text { get; set; } = "";

    /// <summary>False renders struck through with the reason.</summary>
    public bool Accepted { get; set; } = true;

    /// <summary>Required when <see cref="Accepted"/> is false.</summary>
    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}

/// <summary>
/// One rung of the ladder.
///
/// All five rows are written when the item is created, whether or not they carry an amount.
/// That makes "empty stages render as visible empty, never omitted" a property of the DATA
/// rather than a convention the UI has to remember — and it means a reader can always see
/// that four of five rungs are empty, which is usually the story.
/// </summary>
public class MoneyStageEntry
{
    public Guid Id { get; set; }

    public Guid MoneyItemId { get; set; }
    public MoneyItem? MoneyItem { get; set; }

    public FundingStage Stage { get; set; }

    public decimal? AmountUsd { get; set; }

    public StageApplicability Applicability { get; set; } = StageApplicability.EmptyPending;

    /// <summary>Required when <see cref="Applicability"/> is NotApplicable — "a stage that
    /// does not apply says so" (design 1s).</summary>
    [MaxLength(300)]
    public string? NotApplicableReason { get; set; }

    public DateTime? AsOf { get; set; }

    public Guid? SourceRefId { get; set; }

    /// <summary>The law or instrument that moved it to this stage.</summary>
    [MaxLength(200)]
    public string? EnactedByPolicyRef { get; set; }
}
