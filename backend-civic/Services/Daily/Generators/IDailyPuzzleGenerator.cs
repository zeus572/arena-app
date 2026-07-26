using Civic.API.Models.Daily;

namespace Civic.API.Services.Daily.Generators;

/// <summary>
/// Produces one day's puzzle for one game kind. Implementations must be pure selection
/// over already-ingested content wherever possible — play is always zero-LLM, and
/// generation should be too except where a spec explicitly allows a fallback.
///
/// Returning null means "no eligible content today". That is a normal outcome, not an
/// error: the hub simply shows fewer games.
/// </summary>
public interface IDailyPuzzleGenerator
{
    DailyGameKind Kind { get; }

    /// <summary>
    /// True for kinds where a bad puzzle is publicly visible and can read as partisan
    /// (Fork, Time Machine) — those land in Draft for a human to approve. Everything else
    /// auto-approves, since it is pure selection from already-reviewed rows.
    /// </summary>
    bool RequiresReview { get; }

    /// <summary>
    /// Build a puzzle for <paramref name="date"/>, or null when no eligible content exists.
    /// Id, Edition and Status are assigned by the generation host — set everything else.
    /// </summary>
    Task<DailyPuzzle?> GenerateAsync(DateOnly date, CancellationToken ct);
}
