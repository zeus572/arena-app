using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// The source hierarchy from PRD 07 §4, in descending directness.
///
/// This is a description of WHAT a source is, never an assessment of whether it is
/// trustworthy. PRD 07 §4 opens by requiring the two be stored and displayed separately,
/// so there is deliberately no "credibility" or "bias" field on <see cref="SourceRef"/>.
/// </summary>
public enum SourceType
{
    /// <summary>Statutes, bills, court opinions, transcripts, original research.</summary>
    PrimaryDocument,
    /// <summary>Official datasets and government reports.</summary>
    GovernmentData,
    /// <summary>Press releases, speeches, identified-actor posts. Establishes what an
    /// actor SAID — never that the thing said is true.</summary>
    DirectStatement,
    /// <summary>Original, wire, local or specialist reporting.</summary>
    Reporting,
    /// <summary>Academic work, think tanks, expert commentary, opinion journalism.</summary>
    Analysis,
    /// <summary>Public reaction. Never verifies a factual claim.</summary>
    PublicReaction,
}

/// <summary>Whether the source is still reachable and still stands (PRD 04 §14.3).</summary>
public enum SourceAvailability
{
    Live,
    /// <summary>The publisher retracted it. Every claim resting on it needs review.</summary>
    Retracted,
    /// <summary>Temporarily unreachable — paywall, outage, redirect loop.</summary>
    Unavailable,
    /// <summary>Gone for good.</summary>
    Removed,
}

/// <summary>
/// A citable document. One row per URL; reused by every claim, room and money item that
/// cites it, which is the whole point — a retraction has to be able to find them all.
/// </summary>
public class SourceRef
{
    public Guid Id { get; set; }

    [Required, MaxLength(1000)]
    public string Url { get; set; } = "";

    /// <summary>SHA-256 of the normalized URL. Unique — this is what makes re-citing the
    /// same document idempotent instead of forking the graph.</summary>
    [Required, MaxLength(64)]
    public string UrlHash { get; set; } = "";

    [Required, MaxLength(500)]
    public string Title { get; set; } = "";

    [MaxLength(200)]
    public string? Author { get; set; }

    /// <summary>Issuing body or publisher.</summary>
    [MaxLength(200)]
    public string? Organization { get; set; }

    public SourceType SourceType { get; set; } = SourceType.Reporting;

    /// <summary>Rendered next to, but separately from, the source type. See the enum docs.</summary>
    public bool IsPrimary { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(60)]
    public string? Jurisdiction { get; set; }

    [MaxLength(10)]
    public string Language { get; set; } = "en";

    /// <summary>Reproduction and display constraints (PRD 04 §8.2).</summary>
    [MaxLength(500)]
    public string? RightsNote { get; set; }

    /// <summary>False for the RSS-derived majority: Civersify stores headline + summary only,
    /// so most reporting sources cannot supply an exact supporting passage.</summary>
    public bool FullTextAvailable { get; set; }

    public SourceAvailability Availability { get; set; } = SourceAvailability.Live;

    public DateTime? LastCheckedAt { get; set; }

    /// <summary>The source has a stake in the claim it is being used to support (PRD 07 §5).</summary>
    public bool HasInterest { get; set; }

    [MaxLength(300)]
    public string? InterestNote { get; set; }

    /// <summary>Back-pointer when this source came out of the existing news pipeline.</summary>
    public Guid? SourceNewsItemId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
