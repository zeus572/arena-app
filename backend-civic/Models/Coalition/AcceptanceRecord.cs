using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A player's accept/decline of a specific <see cref="ProvisionVersion"/>, with
/// intensity. The acceptance set of a player is the set of versions they would
/// co-sign; coalitions are intersections of these sets that also span the
/// spectrum (doc 06). One record per (user, version).
/// </summary>
public class AcceptanceRecord
{
    public Guid Id { get; set; }

    // Denormalized provision id so acceptance can be queried per-provision
    // without joining through versions.
    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    public Guid VersionId { get; set; }
    public ProvisionVersion? Version { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>True = would co-sign this version; false = explicitly declines it.</summary>
    public bool Accept { get; set; }

    /// <summary>
    /// Intensity of the (non-)acceptance. A decline anchored to a NonNegotiable
    /// position is principled dissent, not a failed bridge; an accept that gave
    /// up a high-intensity position is a costly (valuable) bridge.
    /// </summary>
    public AnswerIntensity Intensity { get; set; } = AnswerIntensity.Medium;

    [MaxLength(120)]
    public string? ReasoningTag { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
