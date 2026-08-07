using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// The eight evidence statuses (PRD 07 §6.1, rendered by design 1m).
///
/// EXACTLY these eight, in this order — the design's mark vocabulary is one square per
/// member and <c>ClaimStatusTests</c> asserts both the count and the member names, because
/// they are persisted as strings and a rename would silently re-label historical claims.
///
/// PRD 03 §6.4 lists a shorter seven-value set; PRD 07 is the cross-cutting standard and wins.
/// </summary>
public enum ClaimStatus
{
    /// <summary>Multiple independent sources, or a primary document.</summary>
    Confirmed,
    /// <summary>Good evidence, nothing contradicting it, not independently confirmed.</summary>
    StronglySupported,
    /// <summary>Could be true; the evidence that would settle it does not exist yet.</summary>
    PlausibleButUnresolved,
    /// <summary>Credible sources directly contradict each other.</summary>
    Disputed,
    /// <summary>Circulating with no evidence behind it.</summary>
    Unsupported,
    /// <summary>Evidence shows it is not true. Retained, never deleted.</summary>
    False,
    /// <summary>Was accurate; something changed. Show <see cref="Claim.StaleAsOf"/>.</summary>
    Outdated,
    /// <summary>A statement about the future, not a fact.</summary>
    Prediction,
}

/// <summary>
/// The epistemic label from PRD 07 §3.2, orthogonal to <see cref="ClaimStatus"/>.
///
/// This is the answer key for the Fact / Opinion / Interpretation / Prediction interaction,
/// which is why it lives on the claim rather than being re-authored per interaction.
/// </summary>
public enum ClaimKind
{
    Factual,
    Interpretation,
    Opinion,
    Prediction,
}

/// <summary>Why a claim's status moved. Drives the changelog row's type column (design 1d).</summary>
public enum StatusChangeKind
{
    InitialReview,
    NewEvidence,
    /// <summary>A material factual correction. Never folded into "updated".</summary>
    Correction,
    Retraction,
    /// <summary>A Prediction-status claim resolved.</summary>
    Resolution,
    /// <summary>Moved to Outdated because the world changed, not because we were wrong.</summary>
    Staleness,
}

/// <summary>
/// A single sourceable assertion, as a first-class object rather than a sentence inside a page.
///
/// The status lives here and ONLY here. Room and story copy must never cache a claim's
/// status into a column of their own — rendering the mark from this row is what makes the
/// automatic half of correction fan-out automatic.
///
/// False and Unsupported claims are retained forever (design 1m): the ledger's job is to
/// record that the claim exists and what the evidence does about it.
/// </summary>
public class Claim
{
    public Guid Id { get; set; }

    /// <summary>Claims are cited and shared, so they are user-addressable.</summary>
    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    /// <summary>SHA-256 of the normalized claim text. Unique — the dedup key for extraction,
    /// and the reason two rooms describing the same fact converge on one row.</summary>
    [Required, MaxLength(64)]
    public string NormalizedTextHash { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Text { get; set; } = "";

    public ClaimKind Kind { get; set; } = ClaimKind.Factual;

    public ClaimStatus Status { get; set; } = ClaimStatus.PlausibleButUnresolved;

    /// <summary>One line on what the evidence actually does — rendered under the claim in
    /// the ledger (design 1n), where it is the difference between a table and an argument.</summary>
    [MaxLength(1000)]
    public string? EvidenceSummary { get; set; }

    /// <summary>
    /// Required by design 1n. A claim nobody can say how to settle is not a claim, it is a
    /// mood — so this is [Required] on the model rather than a UI hint.
    /// </summary>
    [Required, MaxLength(1000)]
    public string WhatWouldSettleIt { get; set; } = "";

    [MaxLength(200)]
    public string? Predicate { get; set; }

    [MaxLength(200)]
    public string? ObjectValue { get; set; }

    public DateTime? TimeScopeStart { get; set; }
    public DateTime? TimeScopeEnd { get; set; }

    [MaxLength(120)]
    public string? GeographyScope { get; set; }

    /// <summary>0..1, from the extraction pass. Not a substitute for <see cref="Status"/>.</summary>
    public double Confidence { get; set; }

    /// <summary>Shown on an Outdated claim: the date after which it stopped being accurate.</summary>
    public DateTime? StaleAsOf { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReviewedAt { get; set; }

    [MaxLength(120)]
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// How many people have seen a share card carrying this claim's wording.
    ///
    /// Share cards render live, so a corrected claim corrects itself everywhere — but there
    /// is no recall mechanism for the people who already read the old wording, and inventing
    /// a fake one would be worse than counting honestly. Design 1z records the number.
    /// </summary>
    public int ShareImpressionCount { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public List<FieldProvenance> Provenance { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One status transition. Kept forever: design 1n gives "history of this label" its own
/// cell in the expanded row, and PRD 07 §6 requires the internal history be permanent.
/// </summary>
public class ClaimStatusHistory
{
    public Guid Id { get; set; }

    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }

    /// <summary>Null for the first entry.</summary>
    public ClaimStatus? FromStatus { get; set; }

    public ClaimStatus ToStatus { get; set; }

    public StatusChangeKind ChangeKind { get; set; } = StatusChangeKind.InitialReview;

    [Required, MaxLength(1000)]
    public string Rationale { get; set; } = "";

    /// <summary>The source that triggered the move.</summary>
    public Guid? TriggerSourceRefId { get; set; }

    /// <summary>
    /// When the ORIGINAL source issued its correction — not when we noticed.
    ///
    /// The published service-level metric (design 1z) is time-from-source-correction, which
    /// cannot be derived from anything we observe. It has to be entered, so the correction
    /// form requires it.
    /// </summary>
    public DateTime? SourceCorrectedAt { get; set; }

    [Required, MaxLength(120)]
    public string ChangedBy { get; set; } = "";

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
