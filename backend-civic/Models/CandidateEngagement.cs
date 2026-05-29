using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>A signed-in user following a candidate. Unique per (UserId, CandidateId).</summary>
public class CandidateFollow
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid CandidateId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A signed-in user muting a candidate. Unique per (UserId, CandidateId).</summary>
public class CandidateMute
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid CandidateId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
