using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// An amendment: a player's free-form proposal of a carve-out/clause that would
/// move a provision into more acceptance sets (the distance-moving engine, doc
/// 06). An amendment proposes a modified <see cref="ProvisionVersion"/>.
/// </summary>
public class Amendment
{
    public Guid Id { get; set; }

    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    /// <summary>The amendment in the player's own words (free-form surface).</summary>
    [Required, MaxLength(4000)]
    public string FreeFormText { get; set; } = "";

    /// <summary>
    /// The version this amendment proposes (the modified configuration). Nullable
    /// because the proposed version may be materialized in a second step.
    /// </summary>
    public Guid? ProposedVersionId { get; set; }
    public ProvisionVersion? ProposedVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
