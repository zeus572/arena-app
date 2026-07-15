using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// Which legislative body a bill belongs to. Federal is the only jurisdiction
/// ingested today; State/Local are the seam for a later expansion (OpenStates /
/// LegiScan adapters) and share the same synthesis + UX pipeline.
/// </summary>
public enum BillJurisdiction
{
    Federal,
    State,
    Local,
}

/// <summary>Where a bill sits in the legislative process. Kept coarse and source-agnostic.</summary>
public enum BillStatus
{
    Introduced,
    InCommittee,
    PassedOneChamber,
    PassedBothChambers,
    Enacted,
    Failed,
    Unknown,
}

/// <summary>
/// Pipeline state for LLM value-synthesis, mirroring <see cref="NewsItemStatus"/>.
/// A bill is ingested first, then synthesized into per-axis positions.
/// </summary>
public enum BillSynthesisStatus
{
    /// <summary>Persisted after ingestion; awaiting value synthesis.</summary>
    Ingested,
    /// <summary>Synthesis in flight.</summary>
    Synthesizing,
    /// <summary>Per-axis positions written.</summary>
    Synthesized,
    /// <summary>Synthesis failed after the retry budget.</summary>
    Failed,
    /// <summary>Manually skipped or judged non-substantive.</summary>
    Skipped,
}

/// <summary>
/// A real piece of legislation. Ingested from a seed list and/or the Congress.gov
/// API (idempotent by <see cref="ExternalId"/>), then positioned on the Civic
/// Compass axes by an LLM (<see cref="AxisPositions"/>). Modeled on
/// <see cref="VirtualCandidate"/> + <see cref="CandidateAxisScore"/>.
/// </summary>
public class Bill
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable upstream id, e.g. "hr-1234-118". Indexed unique so re-ingesting the
    /// same bill is a no-op.
    /// </summary>
    [Required, MaxLength(120)]
    public string ExternalId { get; set; } = "";

    /// <summary>Congress number for federal bills (e.g. 118). 0 for non-federal.</summary>
    public int Congress { get; set; }

    /// <summary>Bill type code as used upstream, e.g. "HR", "S", "HJRES".</summary>
    [Required, MaxLength(12)]
    public string BillType { get; set; } = "";

    /// <summary>Bill number within its type/congress.</summary>
    public int Number { get; set; }

    [Required, MaxLength(500)]
    public string Title { get; set; } = "";

    [MaxLength(300)]
    public string? ShortTitle { get; set; }

    /// <summary>Plain-language summary of what the bill does (from source or LLM).</summary>
    [Required]
    public string Summary { get; set; } = "";

    [MaxLength(160)]
    public string Sponsor { get; set; } = "";

    /// <summary>Sponsor party abbreviation (e.g. "D", "R", "I"), or null if unknown.</summary>
    [MaxLength(20)]
    public string? Party { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Introduced;

    public DateTime IntroducedDate { get; set; }
    public DateTime? LatestActionDate { get; set; }

    [MaxLength(800)]
    public string? FullTextUrl { get; set; }

    [MaxLength(800)]
    public string? SourceUrl { get; set; }

    public BillJurisdiction Jurisdiction { get; set; } = BillJurisdiction.Federal;

    /// <summary>State code / city name for non-federal bills; null for federal.</summary>
    [MaxLength(60)]
    public string? JurisdictionRegion { get; set; }

    // ---- Synthesis pipeline ----

    public BillSynthesisStatus SynthesisStatus { get; set; } = BillSynthesisStatus.Ingested;

    /// <summary>One-paragraph neutral synthesis: what the bill does and the core tradeoff.</summary>
    [MaxLength(2000)]
    public string? SynthesisSummary { get; set; }

    public DateTime? SynthesizedAt { get; set; }

    /// <summary>Surface for the last failed synthesis attempt, when applicable.</summary>
    [MaxLength(2000)]
    public string? LastError { get; set; }

    public int AttemptCount { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;

    public List<BillAxisPosition> AxisPositions { get; set; } = new();
}

/// <summary>
/// Where a bill pushes on one Civic Compass axis, as synthesized by the LLM.
/// One row per relevant axis (axes the bill does not implicate are omitted).
/// </summary>
public class BillAxisPosition
{
    public Guid Id { get; set; }

    public Guid BillId { get; set; }
    public Bill? Bill { get; set; }

    [Required, MaxLength(60)]
    public string AxisKey { get; set; } = "";

    /// <summary>-1.0 = pushes toward the axis low end, +1.0 = toward the high end.</summary>
    public double Score { get; set; }

    /// <summary>0..1 confidence in this positioning.</summary>
    public double Confidence { get; set; }

    /// <summary>One-sentence explanation of why the bill lands here on this axis.</summary>
    [Required, MaxLength(600)]
    public string Rationale { get; set; } = "";

    /// <summary>Short supporting quote or section reference, when available.</summary>
    [MaxLength(600)]
    public string? Evidence { get; set; }
}
