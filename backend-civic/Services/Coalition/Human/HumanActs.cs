using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;

namespace Civic.API.Services.Coalition.Human;

// =====================================================================
// LAYER 2H — human gameplay. Same machine, new input source (principle A6).
// Human daily acts translate to the SAME LoopActs the agents emit; the state
// machine never knows or cares who acted. Pure translation — no LLM.
// =====================================================================

public abstract record HumanAct(string UserId);

/// <summary>Position + intensity (+ optional reasoning tag) — the OPEN-state daily act.</summary>
public sealed record HumanPosition(string UserId, string Stance, AnswerIntensity Intensity, string? ReasoningTag = null) : HumanAct(UserId);

/// <summary>Propose an amendment (a modified version).</summary>
public sealed record HumanAmendment(string UserId, VersionPoint Version) : HumanAct(UserId);

/// <summary>Co-sign a version (accept).</summary>
public sealed record HumanCoSign(string UserId, VersionPoint Version, AnswerIntensity Intensity) : HumanAct(UserId);

/// <summary>Decline a version (with optional reasoning).</summary>
public sealed record HumanDecline(string UserId, VersionPoint Version, AnswerIntensity Intensity, string? ReasoningTag = null) : HumanAct(UserId);

/// <summary>Reaction-with-reason — broadcast engagement; records reasoning points, no geometry change.</summary>
public sealed record HumanReactionWithReason(string UserId, string Reason) : HumanAct(UserId);

/// <summary>Steelman the other side — broadcast engagement; no geometry change.</summary>
public sealed record HumanSteelman(string UserId, string Text) : HumanAct(UserId);

/// <summary>
/// Translates a human daily act into the loop's act vocabulary. Geometry-affecting
/// acts (position / amend / co-sign / decline) map onto the exact same LoopActs
/// agents emit; reaction-with-reason and steelman are broadcast engagement only and
/// produce no LoopAct (they earn reasoning points but don't move the geometry).
/// </summary>
public static class HumanActTranslator
{
    public static LoopAct? ToLoopAct(HumanAct act) => act switch
    {
        HumanPosition p => new TakePositionAct(p.UserId, p.Stance, p.Intensity, p.ReasoningTag),
        HumanAmendment a => new ProposeAmendmentAct(a.UserId, a.Version),
        HumanCoSign c => new CastAcceptanceAct(c.UserId, c.Version, true, c.Intensity),
        HumanDecline d => new CastAcceptanceAct(d.UserId, d.Version, false, d.Intensity),
        HumanReactionWithReason => null,
        HumanSteelman => null,
        _ => null,
    };

    /// <summary>True for acts that are broadcast engagement only (no state transition).</summary>
    public static bool IsEngagementOnly(HumanAct act) => ToLoopAct(act) is null;
}
