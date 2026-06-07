namespace Civic.API.Services.Coalition.Geometry;

/// <summary>A configuration a player rejected earlier and accepted later.</summary>
public sealed record MovedConfiguration(string Canonical, DateTime? RejectedAt, DateTime? AcceptedAt);

public sealed record MovementResult(bool Moved, IReadOnlyList<MovedConfiguration> Movements);

/// <summary>
/// Phase 1.2 — movement detection. Pure computation (no LLM).
///
/// Movement (doc 06, redefined and cheaper to detect): a player's acceptance set
/// EXPANDED to include a configuration they earlier REJECTED. Discrete, logged,
/// honest: rejected configuration X at t1 -> accepted the same configuration at
/// t2 > t1. The geometry only detects THAT movement happened; whether the
/// amendment that moved them was a substantive concession is an LLM gate in a
/// later layer, not here.
/// </summary>
public static class MovementDetector
{
    /// <summary>
    /// Detects movement from a player's chronological accept/decline signals. A
    /// configuration is identified by its canonical point (so re-accepting the same
    /// resolved positions counts, regardless of version id). When timestamps are
    /// absent, input order is treated as chronological.
    /// </summary>
    public static MovementResult DetectFromSignals(IEnumerable<AcceptanceSignal> signals)
    {
        // Stable order: by timestamp when present, else preserve input order.
        var ordered = signals
            .Select((s, i) => (s, i))
            .OrderBy(x => x.s.At ?? DateTime.MinValue)
            .ThenBy(x => x.i)
            .Select(x => x.s)
            .ToList();

        var movements = new List<MovedConfiguration>();
        var byConfig = ordered.GroupBy(s => s.Version.Canonical(), StringComparer.Ordinal);

        foreach (var group in byConfig)
        {
            DateTime? firstRejectAt = null;
            var sawRejectBeforeAccept = false;
            foreach (var s in group)
            {
                if (!s.Accept)
                {
                    firstRejectAt ??= s.At;
                    sawRejectBeforeAccept = true;
                }
                else if (sawRejectBeforeAccept)
                {
                    // accept that follows an earlier reject of the same config = movement
                    movements.Add(new MovedConfiguration(group.Key, firstRejectAt, s.At));
                    break;
                }
            }
        }

        return new MovementResult(movements.Count > 0, movements);
    }

    /// <summary>
    /// Direct region form: did the player's acceptance region EXPAND to now include
    /// a version it previously excluded? (before rejected it, after accepts it.)
    /// </summary>
    public static bool RegionExpandedToInclude(
        AcceptanceRegion before, AcceptanceRegion after, VersionPoint version)
        => !before.Contains(version) && after.Contains(version);
}
