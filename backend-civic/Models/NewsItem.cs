using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum NewsItemStatus
{
    /// <summary>Persisted after ingestion; awaiting content generation.</summary>
    Ingested,
    /// <summary>Content generation in flight.</summary>
    Generating,
    /// <summary>Briefing/ThinkDeeper (and optional Concept/Quiz) written.</summary>
    Generated,
    /// <summary>Content generation failed after the configured retry budget.</summary>
    Failed,
    /// <summary>Manually skipped (e.g. judged off-topic by ops).</summary>
    Skipped,
}

public class NewsItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// Stable upstream id (RSS guid or, when missing, the canonical URL).
    /// Indexed unique so re-ingesting the same headline is a no-op.
    /// </summary>
    [Required, MaxLength(600)]
    public string ExternalId { get; set; } = "";

    [Required, MaxLength(400)]
    public string Headline { get; set; } = "";

    [Required, MaxLength(60)]
    public string Source { get; set; } = "";

    /// <summary>
    /// Real publisher of the story when <see cref="Source"/> is an aggregator
    /// channel (e.g. "Google News Politics" → "NPR"). Null when Source itself
    /// is the publisher. Display preference is <c>Publisher ?? Source</c>.
    /// </summary>
    [MaxLength(120)]
    public string? Publisher { get; set; }

    [Required, MaxLength(800)]
    public string Url { get; set; } = "";

    [MaxLength(2000)]
    public string? Summary { get; set; }

    public DateTime PublishedAt { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public NewsItemStatus Status { get; set; } = NewsItemStatus.Ingested;

    /// <summary>
    /// Local-news region this item was ingested for (2-letter state code), or
    /// null for national feeds. Set from which source feed produced it. See
    /// <see cref="Localities"/>.
    /// </summary>
    [MaxLength(2)]
    public string? Locality { get; set; }

    /// <summary>Surface for the last failed generation attempt, when applicable.</summary>
    [MaxLength(2000)]
    public string? LastError { get; set; }

    public int AttemptCount { get; set; }
}

public static class CivicGenerationSource
{
    public const string Seed = "seed";
    public const string News = "news";
    public const string Manual = "manual";
}
