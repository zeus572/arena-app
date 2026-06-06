using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// Lifecycle state of a provision. The transitions themselves (the state
/// machine) are Layer 2 work; Layer 0 only persists the field so engagement
/// data can hang off a provision in any state.
/// </summary>
public enum ProvisionState
{
    Birth,
    Open,
    Contested,
    NearCoalition,
    Passed,
    Forked,
    Died,
}

/// <summary>
/// The core content unit of the coalition game. A provision is born (usually
/// system-extracted from a <see cref="Briefing"/>) as a neutral, real-tradeoff
/// proposition, then accrues structured engagement: positions, amendments,
/// versions and acceptance records.
///
/// Architectural notes (see docs 03/06 + 07 Part A):
///  - <see cref="NeutralText"/> is the free-form surface (A2). Structure is
///    extracted, not authored.
///  - <see cref="SubQuestions"/> are the latent dimensions of disagreement (A3)
///    and are EMERGENT (A4): more rows can be inserted after birth with no
///    schema change.
///  - <see cref="RelevantAxes"/> is the one-LLM-call-at-birth Values-axis tag
///    used later to measure breadth (distance lives in sub-question space, not
///    here).
/// </summary>
public class Provision
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(300)]
    public string Title { get; set; } = "";

    /// <summary>
    /// Neutral-surface, real-tradeoff provision text. Free-form (players read
    /// and write natural language); structure is extracted from versions, not
    /// from this field. Populated at birth in Phase 0.2.
    /// </summary>
    public string NeutralText { get; set; } = "";

    // Source linkage. System-extracted provisions point at the briefing they
    // were born from; user-authored provisions leave these null.
    public Guid? SourceBriefingId { get; set; }

    [MaxLength(160)]
    public string? SourceBriefingSlug { get; set; }

    public ProvisionState State { get; set; } = ProvisionState.Birth;

    /// <summary>
    /// Relevant Values axes (e.g. "liberty-vs-order"). Tagged by one LLM call
    /// at birth. Stored as a Postgres text[] (no migration needed to change the
    /// set on a row). Breadth is measured against these; distance is not.
    /// </summary>
    public string[] RelevantAxes { get; set; } = Array.Empty<string>();

    /// <summary>Target resolution time (~1 week after birth). The deadline can
    /// send any active state to DIED later (Layer 2).</summary>
    public DateTime? Deadline { get; set; }

    // Provenance: which pipeline produced this row, mirroring the rest of the
    // civic catalog ("seed" | "news" | "manual").
    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation collections.
    public List<SubQuestion> SubQuestions { get; set; } = new();
    public List<ProvisionPosition> Positions { get; set; } = new();
    public List<Amendment> Amendments { get; set; } = new();
    public List<ProvisionVersion> Versions { get; set; } = new();
    public List<AcceptanceRecord> AcceptanceRecords { get; set; } = new();
}
