using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>Verdict of the discrete integrity gates on a near-coalition plank.</summary>
public sealed record GateReport(bool Teeth, bool Moved, int MovedSigners, bool Passed);

/// <summary>
/// Phase 2.3 — the discrete integrity gates (the only place LLM judgment would
/// fire, near-coalition only — A5). The PRIMARY enforcement here is STRUCTURAL and
/// pure:
///  - substantive? an amendment that does not change the extracted vector is a
///    cosmetic restatement;
///  - teeth? the plank must resolve at least a minimum number of sub-questions;
///  - moved? at least one signer bargained in (declined an earlier version first).
/// Semantic refinements ("restatement in different words", "constrains a real
/// institution") are the deferred LLM seam and would only TIGHTEN these, never
/// loosen them.
/// </summary>
public static class IntegrityGates
{
    /// <summary>An amendment is substantive iff it changes the extracted vector (else it's a cosmetic restatement).</summary>
    public static bool IsSubstantive(VersionPoint prior, VersionPoint amended)
        => prior.Canonical() != amended.Canonical();

    /// <summary>A plank has teeth iff it resolves at least <paramref name="minSpecificity"/> sub-questions.</summary>
    public static bool HasTeeth(VersionPoint plank, int minSpecificity = 1)
        => plank.Specificity >= minSpecificity;

    /// <summary>How many of the signers moved (bargained in) to reach this plank.</summary>
    public static int CountMovedSigners(ProvisionLoopState state, VersionPoint plank, IEnumerable<string> signerIds)
        => signerIds.Count(id => LoopMovement.MovedToward(state.SignalsFor(id), plank));

    /// <summary>Run the teeth + movement gates for a plank and its signers (substantive is per-amendment).</summary>
    public static GateReport Evaluate(
        ProvisionLoopState state, VersionPoint plank, IReadOnlyList<string> signerIds, int minTeeth = 1)
    {
        var teeth = HasTeeth(plank, minTeeth);
        var moved = CountMovedSigners(state, plank, signerIds);
        var movedOk = moved >= 1;
        return new GateReport(teeth, movedOk, moved, teeth && movedOk);
    }
}
