using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>
/// Phase 2.1 — the Part B coalition state machine. Pure: transitions are driven
/// by acts + Layer 1 geometry, NO LLM. One machine, two kinds of player (A6):
/// agents and humans emit the same <see cref="LoopAct"/>s.
///
///   BIRTH -> OPEN -> CONTESTED -> NEAR-COALITION -> { PASSED | FORKED | DIED }
///
/// - OPEN -> CONTESTED: enough positions AND a real disagreement (an
///   irreconcilable sub-question) exist.
/// - CONTESTED -> NEAR: a version sits in enough acceptance regions AND its
///   supporters are spectrum-broad (geometry over regions).
/// - CONTESTED/NEAR -> FORKED: two non-overlapping broad basins.
/// - NEAR -> PASSED: explicit acceptances of one version cover all required
///   signers AND it has teeth AND signers moved.
/// - any active -> DIED: the deadline passes without a pass.
/// </summary>
public sealed class ProvisionStateMachine
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    /// <summary>Apply an act, then recompute the state from acts + geometry. Returns the (possibly new) state.</summary>
    public ProvisionState Apply(ProvisionLoopState s, LoopAct act)
    {
        if (s.IsTerminal) return s.State;

        switch (act)
        {
            case TakePositionAct p:
                if (s.PlayerOrNull(p.ActorId) is null)
                    throw new InvalidOperationException($"Unknown actor '{p.ActorId}' took a position.");
                s.Positioned.Add(p.ActorId);
                s.Now += Tick;
                break;

            case ProposeAmendmentAct a:
                if (!s.Versions.Any(v => v.Canonical() == a.Version.Canonical()))
                    s.Versions.Add(a.Version);
                s.Now += Tick;
                break;

            case CastAcceptanceAct c:
                s.Acceptances.Add(new LoopAcceptance(c.ActorId, c.Version, c.Accept, c.Intensity, s.Now));
                s.Now += Tick;
                break;

            case AdvanceToDeadlineAct:
                if (s.Deadline.HasValue) s.Now = s.Deadline.Value + Tick;
                break;
        }

        Recompute(s);
        return s.State;
    }

    private static void Recompute(ProvisionLoopState s)
    {
        if (s.IsTerminal) return;

        var required = s.RequiredPlayers;
        var deadlineHit = s.Deadline.HasValue && s.Now >= s.Deadline.Value;

        // PASS takes priority (an approved, toothful, moved-to plank resolves the provision
        // even at the deadline).
        var pass = EvaluatePass(s, required);
        if (pass is not null && s.State is ProvisionState.NearCoalition or ProvisionState.Contested)
        {
            s.State = ProvisionState.Passed;
            s.Outcome = pass;
            return;
        }

        var fork = ForkDetector.Detect(s.Versions, required, s.Spectrum, s.Config.ForkOptions);

        switch (s.State)
        {
            case ProvisionState.Open:
                if (deadlineHit) { Die(s, "deadline reached in OPEN (no spread formed)"); return; }
                // "Meaningful spread" = enough participants have engaged AND there is a
                // concrete version on the table to work on. Whether they agree or
                // disagree is then resolved by the CONTESTED-stage geometry below.
                var spread = s.Positioned.Count >= s.Config.MinPositionsForSpread
                             && s.Versions.Count >= 1;
                if (!spread) return;
                s.State = ProvisionState.Contested;
                goto case ProvisionState.Contested;

            case ProvisionState.Contested:
                if (deadlineHit) { Die(s, "deadline reached in CONTESTED (no spanning version)"); return; }
                if (fork.IsFork) { Fork(s, fork); return; }
                if (HasSpanningBroadVersion(s, required)) s.State = ProvisionState.NearCoalition;
                return;

            case ProvisionState.NearCoalition:
                if (fork.IsFork) { Fork(s, fork); return; }
                if (deadlineHit) { Die(s, "deadline reached in NEAR-COALITION (plank not approved)"); return; }
                return;
        }
    }

    /// <summary>Geometry-only: does a TOOTHFUL version sit in enough acceptance regions and span the spectrum?</summary>
    private static bool HasSpanningBroadVersion(ProvisionLoopState s, IReadOnlyList<PlayerGeometry> required)
    {
        // Only versions with teeth count — a silent/toothless catch-all must not
        // trip a "coalition" just because everyone trivially tolerates it.
        var candidates = s.Versions.Where(v => v.Specificity >= s.Config.MinTeethSpecificity).ToList();
        var best = DistanceCalculator.DistanceToCoalition(candidates, required);
        if (best.BestVersion is null || best.Uncovered > s.Config.NearCoalitionMaxUncovered) return false;
        var supporters = OverlapCalculator.Supporters(required, best.BestVersion);
        var breadth = BreadthCalculator.Breadth(supporters, s.Spectrum);
        return breadth.CoveredBuckets >= s.Config.NearCoalitionMinBuckets;
    }

    /// <summary>
    /// PASS = explicit accepting acceptances of a single version cover ALL required
    /// signers, the version has teeth (specificity), and signers moved (reject->accept).
    /// Pass criteria recorded in their proper spaces. Returns the outcome or null.
    /// </summary>
    private static CoalitionOutcome? EvaluatePass(ProvisionLoopState s, IReadOnlyList<PlayerGeometry> required)
    {
        foreach (var v in s.Versions)
        {
            var signers = required.Where(p => s.LatestAcceptance(p.UserId, v) == true).ToList();
            if (signers.Count != required.Count) continue;            // everyone required must have co-signed

            if (v.Specificity < s.Config.MinTeethSpecificity) continue; // teeth

            // Movement (doc 06): a signer "moved" if they declined some earlier
            // version before co-signing this plank (rejected A -> signed amended B),
            // i.e. they didn't just sign something that already matched them. The
            // coalition must show real bargaining, not a pre-formed caucus.
            var movedSigners = signers.Count(p => SignerMovedToward(s, p.UserId, v));
            if (s.Config.RequireMovementToPass && movedSigners < 1) continue;

            var breadth = BreadthCalculator.Breadth(signers, s.Spectrum);
            return new CoalitionOutcome(
                ProvisionState.Passed,
                Plank: v,
                Signers: signers.Select(p => p.UserId).ToList(),
                Breadth: breadth,
                Specificity: v.Specificity,
                MovedSigners: movedSigners);
        }
        return null;
    }

    /// <summary>Did the signer decline some version before co-signing the plank? (bargained in, not pre-matched)</summary>
    private static bool SignerMovedToward(ProvisionLoopState s, string userId, VersionPoint plank)
        => LoopMovement.MovedToward(s.SignalsFor(userId), plank);

    private static void Die(ProvisionLoopState s, string reason)
    {
        s.State = ProvisionState.Died;
        s.Outcome = new CoalitionOutcome(ProvisionState.Died, DiedReason: reason);
    }

    private static void Fork(ProvisionLoopState s, ForkResult fork)
    {
        s.State = ProvisionState.Forked;
        var children = fork.Basins.Take(2).Select((b, i) =>
            new ForkChild($"{s.ProvisionId}#fork{i + 1}", b.Representative, b.SupporterIds)).ToList();
        s.Outcome = new CoalitionOutcome(ProvisionState.Forked, ForkChildren: children);
    }
}
