using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// One row of a Theme Room's "Latest" section (design 1g).
///
/// A Development is meaningful BY CONSTRUCTION — it exists because an editor judged that
/// something changed. Things that are not meaningful are <see cref="ChangeLogEntry"/> rows.
/// Keeping the two in separate tables is what makes both the "what we left out" sidebar and
/// the "11 edits we did not bother you with" footer honest and trivial to compute.
/// </summary>
public class Development
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    public DateTime OccurredAt { get; set; }

    public RoomTopicCategory Category { get; set; } = RoomTopicCategory.Legislative;

    [Required, MaxLength(300)]
    public string Headline { get; set; } = "";

    [MaxLength(500)]
    public string Summary { get; set; } = "";

    /// <summary>
    /// Required on every development (design 1a: "'Why it matters' is a required field").
    /// A dated headline with no consequence attached is the thing Topic Rooms exist to
    /// replace, so this is [Required] on the model rather than a content guideline.
    /// </summary>
    [Required, MaxLength(1000)]
    public string WhyItMatters { get; set; } = "";

    /// <summary>
    /// Which clause of the room's inclusion rule let this in.
    ///
    /// Design 1g prints the seven-part rule beside the list and states the count of what was
    /// excluded. That disclosure is only meaningful if each included item can name its reason,
    /// so this is required too.
    /// </summary>
    [Required, MaxLength(500)]
    public string InclusionReason { get; set; } = "";

    /// <summary>The evidence status shown on the row. For a development backed by a claim
    /// this is kept in step by the propagation service; standalone rows carry their own.</summary>
    public ClaimStatus EvidenceStatus { get; set; } = ClaimStatus.Confirmed;

    /// <summary>The Story Room this development opens into, when one exists.</summary>
    public Guid? StoryRoomId { get; set; }

    public int Ordinal { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
