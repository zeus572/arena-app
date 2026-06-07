using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A specific worded configuration of a provision (base text or an amended
/// variant). Named ProvisionVersion rather than the plan's bare "Version" to
/// avoid colliding with System.Version.
///
/// A2/A3: the <see cref="Text"/> is free-form; the <see cref="ExtractedPositions"/>
/// vector is the structured representation the geometry computes over, produced
/// once by the extraction step (Phase 0.3) at write time.
/// </summary>
public class ProvisionVersion
{
    public Guid Id { get; set; }

    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    /// <summary>Author of this version; null for the system base version.</summary>
    [MaxLength(120)]
    public string? AuthorUserId { get; set; }

    /// <summary>Short human label, e.g. "base" or "size-threshold carve-out".</summary>
    [MaxLength(160)]
    public string? Label { get; set; }

    /// <summary>The free-form version text players read and write.</summary>
    [Required, MaxLength(8000)]
    public string Text { get; set; } = "";

    /// <summary>
    /// SHA-256 of the normalized <see cref="Text"/>. Drives the extraction cache
    /// (A5: extraction is cacheable) and cheap dedup of identical versions.
    /// </summary>
    [Required, MaxLength(64)]
    public string TextHash { get; set; } = "";

    /// <summary>
    /// The extracted sub-question-position vector: map of SubQuestion.Key ->
    /// resolved position label. Stored as jsonb so a version can carry a
    /// position on a sub-question added AFTER this provision's birth without any
    /// schema change (A4). A key absent from the map means the version does not
    /// resolve that sub-question.
    /// </summary>
    public Dictionary<string, string> ExtractedPositions { get; set; } = new();

    /// <summary>Whether <see cref="ExtractedPositions"/> has been populated yet.</summary>
    public bool IsExtracted { get; set; }

    /// <summary>Provenance of the extraction (model id / tier), for auditing fidelity.</summary>
    [MaxLength(80)]
    public string? ExtractionModel { get; set; }

    public DateTime? ExtractedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AcceptanceRecord> AcceptanceRecords { get; set; } = new();
}
