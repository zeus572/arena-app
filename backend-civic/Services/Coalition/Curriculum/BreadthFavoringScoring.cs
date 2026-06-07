namespace Civic.API.Services.Coalition.Curriculum;

/// <summary>A player's contribution tallies over a season.</summary>
public sealed record PlayerContribution(
    string UserId,
    int CoalitionsSigned,
    int TotalBreadthOfSignedCoalitions, // sum of spectrum-buckets-covered across the coalitions they signed
    int MovedCount,                     // how often they bargained in (reject -> accept)
    int RawActs);                       // total acts (volume)

/// <summary>A row in the standings.</summary>
public sealed record StandingRow(string UserId, double Score);

/// <summary>
/// Phase 3.3 — scoring tilted so CROSS-CUTTING play climbs fastest. Breadth (and
/// bridging/movement) are weighted far above raw act volume, so a player who helps
/// assemble a few broad cross-spectrum coalitions outranks one who racks up many
/// same-corner acts. Pure. (Weights are a starting point for calibration.)
/// </summary>
public static class BreadthFavoringScoring
{
    public const double BreadthWeight = 5.0;
    public const double MovementWeight = 3.0;
    public const double VolumeWeight = 0.1; // volume barely counts

    public static double Score(PlayerContribution c) =>
        c.TotalBreadthOfSignedCoalitions * BreadthWeight
        + c.MovedCount * MovementWeight
        + c.RawActs * VolumeWeight;

    /// <summary>Standings, highest score first.</summary>
    public static IReadOnlyList<StandingRow> Standings(IEnumerable<PlayerContribution> contributions) =>
        contributions
            .Select(c => new StandingRow(c.UserId, Score(c)))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.UserId, StringComparer.Ordinal)
            .ToList();
}
