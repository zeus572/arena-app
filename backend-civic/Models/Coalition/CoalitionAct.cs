using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// The acts ladder (doc 02). Daily micro/mid acts earn reasoning XP (low ceiling,
/// diminishing returns); scarce macro/coalition acts earn the premium currency
/// (uncapped). System payout acts deposit on provision resolution.
/// </summary>
public enum CoalitionActType
{
    // daily — micro
    ReactionWithReason,
    ClaimTag,                 // fact / interpretation / prediction / value
    Position,
    // daily — mid
    Steelman,                 // quality-gated
    CultureGovernanceSort,
    ReactAndRoute,            // values vs mechanism disagreement
    CoSign,                   // bare co-sign — worth little (asymmetry guard)
    Amend,                    // co-sign-with-substance — worth real points
    // scarce — macro
    AuthorProvision,
    WritePlank,
    PrincipledDissent,
    Longform,
    // system payouts
    CoalitionPassReward,      // scarce: deposited to each signer when a coalition passes
    DiedReasoningPayout,      // reasoning: dead provisions still pay participants
}

/// <summary>A recorded act in the ledger: what was done, how it scored, and what it paid.</summary>
public class CoalitionAct
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid? ProvisionId { get; set; }

    /// <summary>
    /// The specific provision version this act reacted to (e.g. the prevailing
    /// coalition wording a reaction-with-reason or steelman was aimed at). Null for
    /// acts not tied to a particular version. Judging scores against this version's
    /// text when set, the neutral text otherwise.
    /// </summary>
    public Guid? VersionId { get; set; }

    public CoalitionActType Type { get; set; }

    [MaxLength(4000)]
    public string? Payload { get; set; }

    public int GovernanceScore { get; set; } // 0-100 (judge or heuristic)
    public int QualityScore { get; set; }    // 0-100

    public int Points { get; set; }

    /// <summary>"reasoning" (daily, diminishing, capped) or "scarce" (premium, uncapped).</summary>
    [Required, MaxLength(20)]
    public string Currency { get; set; } = "reasoning";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
