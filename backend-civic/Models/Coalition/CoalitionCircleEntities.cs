using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A composed coalition circle (Layer 3.3): a structured-diverse cohort balanced
/// across the Values spectrum. <see cref="GapTier"/> is the difficulty band (gap
/// width in [0,1]) the circle is served (Layer 3.2). A circle is the skill/engagement
/// cohort a player is laddered through (promote/relegate by tier) — this is the
/// DuoLingo-style grouping, and is DISTINCT from a social <c>League</c> (a private
/// group of friends; see <see cref="League"/>).
/// </summary>
public class CoalitionCircle
{
    public Guid Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = "";

    public double GapTier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CoalitionCircleMember> Members { get; set; } = new();
}

/// <summary>A member of a coalition circle (their spectrum bucket + age band).</summary>
public class CoalitionCircleMember
{
    public Guid Id { get; set; }

    public Guid CircleId { get; set; }
    public CoalitionCircle? Circle { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    [Required, MaxLength(40)]
    public string SpectrumBucket { get; set; } = "";

    [Required, MaxLength(20)]
    public string AgeBand { get; set; } = "Adult";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A day a user was active in the coalition game (Layer 3.4 soft cadence). One row
/// per (user, day); cadence is the recency-weighted coverage over a window.
/// </summary>
public class CoalitionActivityDay
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public DateOnly Day { get; set; }
}
