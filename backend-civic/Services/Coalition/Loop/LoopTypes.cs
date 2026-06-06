using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

// =====================================================================
// LAYER 2 — the coalition loop (Part B state machine). The loop MECHANICS are
// pure: transitions are driven by acts + Layer 1 geometry, with NO LLM calls.
// LLM is confined to clearly-isolated seams used elsewhere (agent-region
// derivation from real Values, free-form text rendering, semantic gate
// refinements) — none of which the state machine itself needs.
// =====================================================================

/// <summary>Tunable thresholds for the loop (recorded assumptions; sane defaults).</summary>
public sealed record LoopConfig(
    int MinPositionsForSpread = 2,       // OPEN -> CONTESTED needs this many positions AND a real disagreement
    int NearCoalitionMaxUncovered = 0,   // CONTESTED -> NEAR: best version may miss at most this many required regions
    int NearCoalitionMinBuckets = 2,     // ...and its supporters must cover at least this many spectrum buckets
    int MinTeethSpecificity = 1,         // a passing plank must resolve at least this many sub-questions
    bool RequireMovementToPass = true,   // all signers must have moved (reject->accept) to pass
    ForkOptions? ForkOptions = null);

/// <summary>A player's accept/decline of a specific version, with intensity and a logical timestamp.</summary>
public sealed record LoopAcceptance(
    string UserId,
    VersionPoint Version,
    bool Accept,
    AnswerIntensity Intensity,
    DateTime At);

// ---- Acts (the only inputs that mutate a provision; agents and humans emit the same acts) ----

public abstract record LoopAct(string ActorId);

/// <summary>OPEN-state act: declare engagement (position + intensity + reasoning tag).</summary>
public sealed record TakePositionAct(string ActorId, string Stance, AnswerIntensity Intensity, string? ReasoningTag = null)
    : LoopAct(ActorId);

/// <summary>CONTESTED-state act: propose a modified version (a new candidate point in sub-question space).</summary>
public sealed record ProposeAmendmentAct(string ActorId, VersionPoint Version)
    : LoopAct(ActorId);

/// <summary>Accept or decline a specific version (records into the acceptance set).</summary>
public sealed record CastAcceptanceAct(string ActorId, VersionPoint Version, bool Accept, AnswerIntensity Intensity)
    : LoopAct(ActorId);

/// <summary>Move the logical clock to/past the deadline (can send any active state to DIED).</summary>
public sealed record AdvanceToDeadlineAct() : LoopAct("system");

/// <summary>A child provision spawned by a FORK (each re-enters CONTESTED around its basin's version).</summary>
public sealed record ForkChild(string ProvisionId, VersionPoint BasinVersion, IReadOnlyList<string> SupporterIds);

/// <summary>
/// The terminal record a provision deposits when it resolves: the pass criteria
/// in their proper spaces (breadth in Values space; specificity + movement in
/// sub-question space) for PASSED, the basins for FORKED, the reason for DIED.
/// </summary>
public sealed record CoalitionOutcome(
    ProvisionState FinalState,
    VersionPoint? Plank = null,
    IReadOnlyList<string>? Signers = null,
    BreadthResult? Breadth = null,
    int Specificity = 0,
    int MovedSigners = 0,
    IReadOnlyList<ForkChild>? ForkChildren = null,
    string? DiedReason = null);
