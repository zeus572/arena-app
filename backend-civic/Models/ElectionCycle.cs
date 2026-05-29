using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A fictional election cycle the Virtual Candidates are campaigning for.
/// Dated ~12-18 months out per the PRD; refreshed periodically.
/// </summary>
public class ElectionCycle
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public DateTime ElectionDate { get; set; }
    public DateTime PrimarySeasonStart { get; set; }
    public DateTime GeneralSeasonStart { get; set; }

    /// <summary>Exactly one cycle is the current one surfaced by the API.</summary>
    public bool IsCurrent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
