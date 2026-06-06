using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// A participant in a provision's coalition loop — an agent (seed/ballast) or a
/// human. Stores the participant's Values-spectrum bucket and, for agents, their
/// acceptance region + per-sub-question intensities (the structured projection of
/// their Values onto this provision). Human regions are derived from their
/// AcceptanceRecords at load time, so humans leave the region fields null.
///
/// This is the product-wiring bridge between the pure in-memory loop snapshot and
/// EF persistence.
/// </summary>
public class CoalitionParticipant
{
    public Guid Id { get; set; }

    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    [Required, MaxLength(40)]
    public string SpectrumBucket { get; set; } = "";

    public bool IsAgent { get; set; }

    /// <summary>Agent acceptance region as jsonb: map of sub-question key -> acceptable labels.</summary>
    public string? RegionJson { get; set; }

    /// <summary>Agent intensities as jsonb: map of sub-question key -> AnswerIntensity name.</summary>
    public string? IntensitiesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
