using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A composed coalition league (Layer 3.3): a structured-diverse group balanced
/// across the Values spectrum. <see cref="GapTier"/> is the difficulty band (gap
/// width in [0,1]) the league is served (Layer 3.2).
/// </summary>
public class CoalitionLeague
{
    public Guid Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = "";

    public double GapTier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CoalitionLeagueMember> Members { get; set; } = new();
}

/// <summary>A member of a coalition league (their spectrum bucket + age band).</summary>
public class CoalitionLeagueMember
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }
    public CoalitionLeague? League { get; set; }

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
