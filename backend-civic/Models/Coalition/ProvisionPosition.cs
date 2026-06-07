using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A player's position on a provision — the cheap, high-volume OPEN-state act
/// (position + intensity + reasoning tag). Named ProvisionPosition rather than
/// the plan's bare "Position" to avoid colliding with framework types.
///
/// This is a stance on the provision overall; acceptance of a specific worded
/// configuration is recorded separately as an <see cref="AcceptanceRecord"/>.
/// </summary>
public class ProvisionPosition
{
    public Guid Id { get; set; }

    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    // Civic uses string user ids throughout (anonymous + authed).
    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>Free-form short stance (e.g. "for, but only for large facilities").</summary>
    [Required, MaxLength(600)]
    public string Stance { get; set; } = "";

    /// <summary>
    /// How hard the player holds this stance. NonNegotiable marks a
    /// high-intensity anchor that distinguishes a principled holdout from a
    /// failed bridge (doc 06). Reuses the platform-wide AnswerIntensity scale.
    /// </summary>
    public AnswerIntensity Intensity { get; set; } = AnswerIntensity.Medium;

    /// <summary>Culture-vs-governance (or other) reasoning tag attached to the act.</summary>
    [MaxLength(120)]
    public string? ReasoningTag { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
