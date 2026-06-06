using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>
/// Shared movement rule for the loop (doc 06): a signer "moved" if they declined
/// some earlier version before co-signing the plank (rejected A → signed amended
/// B) — i.e. they bargained in rather than signing something that already matched
/// them. Pure.
/// </summary>
public static class LoopMovement
{
    public static bool MovedToward(IReadOnlyList<AcceptanceSignal> chronologicalSignals, VersionPoint plank)
    {
        var canon = plank.Canonical();
        var acceptAt = chronologicalSignals
            .Where(x => x.Version.Canonical() == canon && x.Accept)
            .Select(x => x.At)
            .FirstOrDefault();
        return chronologicalSignals.Any(x => !x.Accept && (acceptAt is null || x.At < acceptAt));
    }
}
