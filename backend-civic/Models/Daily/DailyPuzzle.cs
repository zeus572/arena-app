using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Daily;

/// <summary>
/// The casual daily games (docs/civic_daily_games). Adding another is an enum member
/// plus a generator + scorer — the storage shape is deliberately generic.
///
/// Members are persisted by ordinal, so new kinds are APPENDED. Reordering would
/// silently re-label every historical puzzle.
/// </summary>
public enum DailyGameKind
{
    /// <summary>Two costly options, one tap. No right answer.</summary>
    Fork,
    /// <summary>Guess what share of people got each question right.</summary>
    CrowdCall,
    /// <summary>Guess the size of a real civic figure; three guesses, higher/lower.</summary>
    PricedIn,
    /// <summary>Guess where a real bill sits on three compass axes.</summary>
    PlaceIt,
    /// <summary>Sort real headlines by era, or spot this week's.</summary>
    TimeMachine,
    /// <summary>Name the value an argument appeals to.</summary>
    WhoseValue,
    /// <summary>Two real figures, one question. Which one is true?</summary>
    WhichIsTrue,
}

/// <summary>
/// Review lifecycle for a generated puzzle. Deterministic kinds auto-approve at
/// generation; Fork and TimeMachine require a human pass (see 00_OVERVIEW §Admin review).
/// </summary>
public enum DailyPuzzleStatus
{
    Draft,
    Approved,
    Live,
    Retired,
}

/// <summary>
/// One day's puzzle for one game kind. Per-game content lives in <see cref="PayloadJson"/>
/// so all six games — and any future ones — share a single table and a single migration.
/// The payload shape for each kind is documented in its spec file and modeled by the
/// records in <c>Models/Daily/Payloads.cs</c>.
/// </summary>
public class DailyPuzzle
{
    public Guid Id { get; set; }

    public DailyGameKind Kind { get; set; }

    /// <summary>The day this is "today's" puzzle. Unique with (Kind, Locality).</summary>
    public DateOnly PuzzleDate { get; set; }

    /// <summary>Human-facing edition number ("Fork #142"). Monotonic per kind.</summary>
    public int Edition { get; set; }

    /// <summary>
    /// Game-specific content INCLUDING the answer key. Never serialize this to a client
    /// directly — <c>DailyPuzzleService</c> strips solution fields for GET responses.
    /// </summary>
    public string PayloadJson { get; set; } = "";

    public int PayloadVersion { get; set; } = 1;

    /// <summary>2-letter state code for locality-scoped variants; null = national.</summary>
    [MaxLength(2)]
    public string? Locality { get; set; }

    // Provenance — the real content this puzzle was cut from.
    public Guid? SourceBillId { get; set; }
    public Guid? SourceProvisionId { get; set; }
    public Guid? SourceNewsItemId { get; set; }

    public DailyPuzzleStatus Status { get; set; } = DailyPuzzleStatus.Draft;

    /// <summary>seed | derived | manual — reuses <see cref="CivicGenerationSource"/>.</summary>
    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One person's play of one puzzle. The unique (PuzzleId, UserId) index is both the
/// "one play each" rule and the idempotency guard for the XP award.
/// </summary>
public class DailyPuzzlePlay
{
    public Guid Id { get; set; }

    public Guid PuzzleId { get; set; }
    public DailyPuzzle? Puzzle { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>What the player answered. Shape per game; see Payloads.cs.</summary>
    public string ResponseJson { get; set; } = "";

    /// <summary>Normalized 0..100. Games with no right answer (Fork) store 0.</summary>
    public int Score { get; set; }

    public int AttemptsUsed { get; set; }

    /// <summary>
    /// False while a multi-guess game (PricedIn) is mid-play. XP is awarded — once —
    /// on the transition to true.
    /// </summary>
    public bool Completed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
