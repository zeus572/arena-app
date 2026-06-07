using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// Persistent cache for the extraction function (Phase 0.3, A5: extraction is
/// "frequent but bounded, cacheable"). Keyed by the normalized text hash AND a
/// signature of the known sub-questions at extraction time — the same text can
/// extract differently once new sub-questions are known, so both are part of the
/// key.
/// </summary>
public class ExtractionCacheEntry
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 (hex) of the normalized version text.</summary>
    [Required, MaxLength(64)]
    public string TextHash { get; set; } = "";

    /// <summary>
    /// Stable signature of the known sub-question set (sorted keys joined). Part
    /// of the cache key because extraction output depends on what was known.
    /// </summary>
    [Required, MaxLength(64)]
    public string KnownSignature { get; set; } = "";

    /// <summary>Serialized <c>ExtractionResult</c> (positions + new sub-questions).</summary>
    [Required]
    public string ResultJson { get; set; } = "";

    [MaxLength(80)]
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
