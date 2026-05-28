using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum ElectionScope
{
    National,
    State,
    Local,
}

public class Election
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    public ElectionScope Scope { get; set; }

    // Date the polls close (UTC). Used to compute the countdown.
    public DateTime ScheduledAt { get; set; }

    // Optional jurisdictional region key for non-national elections
    // (e.g. state code "CA", county slug, district id). Null for national.
    [MaxLength(120)]
    public string? Region { get; set; }

    [MaxLength(600)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
