using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// Interaction templates (PRD 06 §5). The first five are the MVP set; the rest are named
/// so the enum does not have to be reordered later — members are APPEND-only.
/// </summary>
public enum InteractionKind
{
    /// <summary>Pre-exposure commitment. No timer, no streak, no penalty.</summary>
    BeforeYouKnow,
    /// <summary>Fact / Opinion / Interpretation / Prediction, over verbatim sentences.</summary>
    ClassifyStatement,
    TimelineBuilder,
    CalibratedPrediction,
    VoteBeforeReading,
    // Named but not built.
    HeadlineOrHype,
    SourceTrail,
    WhatIsMissing,
    ChartTrap,
    PowerMatch,
    MapChallenge,
    ConsequenceTree,
    BuildAnAmendment,
    CoalitionBuilder,
    BudgetAllocator,
    GuessTheFundingStage,
}

/// <summary>
/// Which side of the reading a response was given on.
///
/// Part of the uniqueness key, because the two-phase interactions legitimately store two
/// rows per person — that is the mechanic, not a duplicate.
/// </summary>
public enum InteractionPhase
{
    /// <summary>Before exposure. For Vote Before Reading this answer is withheld from the
    /// user until they have read both sides.</summary>
    Pre,
    Post,
}

public enum InteractionScoringMode
{
    /// <summary>No right answer. Before You Know and Vote Before Reading are unscored by
    /// design — scoring an opinion would be an ideological answer key.</summary>
    Unscored,
    Exact,
    Partial,
    /// <summary>Proper scoring rule; see PredictionScoring.</summary>
    Brier,
}

/// <summary>
/// A reusable learning object bound to room content (PRD 06 §9.1).
///
/// This reuses the DailyPuzzle PATTERN (generic payload + kind + status) but not the table.
/// DailyPuzzle is keyed (Kind, PuzzleDate, Locality) — inherently one per kind per day —
/// while room interactions are content-scoped, many per room, and must be revalidated when
/// a claim moves. Forcing them into that table would mean a nullable PuzzleDate and a
/// broken unique index.
/// </summary>
public class Interaction
{
    public Guid Id { get; set; }

    /// <summary>Null when the interaction is reusable across rooms.</summary>
    public Guid? RoomId { get; set; }
    public Room? Room { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    public InteractionKind Kind { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(300)]
    public string LearningObjective { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Prompt { get; set; } = "";

    /// <summary>
    /// Options, answer key and per-option explanations. NEVER serialized to a client
    /// directly — InteractionRedaction strips the solution for GET responses, the same way
    /// DailyRedaction does for the daily games.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    public int PayloadVersion { get; set; } = 1;

    /// <summary>Shown after answering, right or wrong. PRD 06 makes an explanation-less
    /// interaction a publish blocker: correctness alone teaches nothing.</summary>
    [MaxLength(2000)]
    public string Explanation { get; set; } = "";

    public InteractionScoringMode ScoringMode { get; set; } = InteractionScoringMode.Unscored;

    public SensitivityLevel Sensitivity { get; set; } = SensitivityLevel.Standard;

    [MaxLength(60)]
    public string? AgeGuidance { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Draft;

    /// <summary>
    /// True when the correct answer depends on a claim's current evidence status.
    ///
    /// This is what makes correction propagation flag the interaction for revalidation
    /// rather than leaving it quietly serving a stale answer key.
    /// </summary>
    public bool AnswerDependsOnClaimStatus { get; set; }

    /// <summary>The room revision the answer key was last validated against (PRD 06 §9.4).</summary>
    public int ContentRevision { get; set; }

    /// <summary>For CalibratedPrediction: the question this is a thin pointer to.</summary>
    public Guid? PredictionId { get; set; }

    public int Ordinal { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public List<FieldProvenance> Provenance { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One person's response to one interaction, in one phase.</summary>
public class RoomInteractionPlay
{
    public Guid Id { get; set; }

    public Guid InteractionId { get; set; }
    public Interaction? Interaction { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public InteractionPhase Phase { get; set; } = InteractionPhase.Post;

    public string ResponseJson { get; set; } = "{}";

    /// <summary>Normalized 0..100. Unscored kinds store 0.</summary>
    public int Score { get; set; }

    /// <summary>The Post row is the idempotency guard for the XP award.</summary>
    public bool Completed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
