using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.Rooms;

/// <summary>
/// Who proposed a piece of content. PRD 07 §7 requires model and prompt version logging
/// on anything the LLM wrote, and the editorial review screen (design 1y) renders a 3px
/// accent rule on any field whose <see cref="FieldProvenance.VerifiedAt"/> is still null.
/// </summary>
public enum ProvenanceOrigin
{
    /// <summary>Drafted by the LLM. Never publishable until a human verifies it.</summary>
    Model,
    /// <summary>Written or confirmed by a person.</summary>
    Human,
    /// <summary>Shipped in an embedded Seed/*.json file.</summary>
    Seed,
}

/// <summary>
/// Per-FIELD provenance, stored as jsonb on the owning entity.
///
/// This is the reason Story Room sections are real columns rather than one PayloadJson
/// blob: PRD 04 §7 requires provenance at the field level, and a field has to be an
/// addressable named thing for that to mean anything.
/// </summary>
public class FieldProvenance
{
    /// <summary>The property name on the owning entity, e.g. "CurrentStatusSentence".</summary>
    [Required, MaxLength(80)]
    public string Field { get; set; } = "";

    public ProvenanceOrigin ProposedBy { get; set; } = ProvenanceOrigin.Human;

    /// <summary>Model id when <see cref="ProposedBy"/> is <see cref="ProvenanceOrigin.Model"/>.</summary>
    [MaxLength(60)]
    public string? ModelId { get; set; }

    public int? PromptVersion { get; set; }

    /// <summary>The source this field's content traces back to. Required by the
    /// ProvenanceComplete publish gate for anything factual.</summary>
    public Guid? SourceRefId { get; set; }

    [MaxLength(120)]
    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }
}
