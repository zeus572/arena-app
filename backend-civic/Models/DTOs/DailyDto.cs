using System.Text.Json.Nodes;

namespace Civic.API.Models.DTOs;

/// <summary>A puzzle as served to a client — payload already redacted of its answer key.</summary>
public class DailyPuzzleDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string PuzzleDate { get; set; } = "";
    public int Edition { get; set; }
    public int PayloadVersion { get; set; }
    public string? Locality { get; set; }

    /// <summary>Redacted payload. Shape per game; see docs/civic_daily_games/.</summary>
    public JsonNode? Payload { get; set; }

    /// <summary>The caller's play, when they have already started or finished this one.</summary>
    public DailyPlayStateDto? Play { get; set; }
}

public class DailyPlayStateDto
{
    public bool Completed { get; set; }
    public int Score { get; set; }
    public int AttemptsUsed { get; set; }
    public JsonNode? Response { get; set; }
}

/// <summary>
/// The weekly ring. Deliberately NOT a breakable streak counter — the gamification docs
/// argue hard streaks "punish the reflective, occasional, busy user".
/// </summary>
public class DailyCadenceDto
{
    /// <summary>Oldest-first, 7 entries ending today.</summary>
    public bool[] Last7Days { get; set; } = new bool[7];
    public int ActiveDays { get; set; }
}

/// <summary>Everything the /daily hub needs in one round-trip.</summary>
public class DailySlateDto
{
    public string Date { get; set; } = "";
    public List<DailyPuzzleDto> Puzzles { get; set; } = new();
    public DailyCadenceDto Cadence { get; set; } = new();

    /// <summary>True when the caller has no stable id, so no XP will be recorded.</summary>
    public bool Anonymous { get; set; }
}

/// <summary>One round/axis/item of feedback in a result.</summary>
public class DailyRoundResultDto
{
    public int Score { get; set; }
    public string Band { get; set; } = "";
}

/// <summary>
/// The result of a submitted play: score, the now-revealed answer key, crowd stats,
/// XP awarded, and the share grid.
/// </summary>
public class DailyResultDto
{
    public Guid PuzzleId { get; set; }
    public string Kind { get; set; } = "";
    public int Edition { get; set; }
    public bool Completed { get; set; }
    public int Score { get; set; }
    public int AttemptsUsed { get; set; }

    public List<DailyRoundResultDto> Rounds { get; set; } = new();

    /// <summary>The answer key + explanations, released only now. Shape per game.</summary>
    public JsonNode? Reveal { get; set; }

    /// <summary>Aggregate stats over other players' plays, where the game has them.</summary>
    public JsonNode? Crowd { get; set; }

    public string ShareGrid { get; set; } = "";

    /// <summary>Reasoning XP awarded (0 for anonymous callers and for replays).</summary>
    public int PointsAwarded { get; set; }
}

/// <summary>Mid-play feedback for the Priced In higher/lower ladder.</summary>
public class PricedInGuessResultDto
{
    public bool Completed { get; set; }
    public int GuessesUsed { get; set; }
    public int GuessesRemaining { get; set; }

    /// <summary>"higher" | "lower" | "exact" — never the true value.</summary>
    public string Direction { get; set; } = "";

    /// <summary>Present only once the play is complete.</summary>
    public DailyResultDto? Result { get; set; }
}

/// <summary>Mid-play feedback for the Place It round ladder.</summary>
public class PlaceItRoundResultDto
{
    public bool Completed { get; set; }
    public int RoundsUsed { get; set; }
    public int RoundsRemaining { get; set; }

    /// <summary>Per-axis: "exact" | "higher" | "lower" — never the true bucket.</summary>
    public string[] Hints { get; set; } = Array.Empty<string>();

    /// <summary>Present only once the play is complete.</summary>
    public DailyResultDto? Result { get; set; }
}

/// <summary>
/// A puzzle as shown to a REVIEWER — payload includes the answer key. Admin-only.
/// </summary>
public class AdminDailyPuzzleDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "";
    public string PuzzleDate { get; set; } = "";
    public int Edition { get; set; }
    public string Status { get; set; } = "";
    public string GenerationSource { get; set; } = "";
    public string? Locality { get; set; }
    public int Plays { get; set; }
    public JsonNode? Payload { get; set; }
}

/// <summary>The bank-balance audit: where an unintended editorial lean would show up.</summary>
public class AdminDailyBalanceDto
{
    /// <summary>Which axis each of the last 30 days' Fork puzzles turned on.</summary>
    public Dictionary<string, int> ForkAxisCounts { get; set; } = new();

    public int MagnitudeTotal { get; set; }
    public int MagnitudeSmallerCount { get; set; }

    /// <summary>Share of magnitudes whose truth is "smaller than you think". Target 0.45–0.55.</summary>
    public double MagnitudeSmallerShare { get; set; }

    /// <summary>Magnitudes whose figure is old enough to need re-verification.</summary>
    public List<string> StaleMagnitudeKeys { get; set; } = new();
}

/// <summary>An archive row: what the caller scored on a past edition.</summary>
public class DailyArchiveRowDto
{
    public Guid PuzzleId { get; set; }
    public int Edition { get; set; }
    public string PuzzleDate { get; set; } = "";
    public bool Played { get; set; }
    public int Score { get; set; }
}
