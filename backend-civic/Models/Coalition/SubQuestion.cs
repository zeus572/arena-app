using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// Where a sub-question came from. Birth sub-questions are identified by the
/// extraction LLM when the provision is born; Emergent ones are surfaced later
/// by an amendment/version that introduces a crux nobody anticipated (A4).
/// </summary>
public enum SubQuestionOrigin
{
    Birth,
    Emergent,
}

/// <summary>
/// A latent dimension of disagreement on a provision (A3) — e.g. for a
/// data-center grid-cost fee: "which facilities are covered?", "marginal or
/// average cost?", "are existing facilities grandfathered?".
///
/// CRITICAL (A4): sub-questions are EMERGENT. A sub-question is just a row here,
/// so a new one can be inserted into an existing provision with a plain INSERT —
/// no migration. A version's resolved positions are stored as a JSON map keyed
/// by <see cref="Key"/> on <see cref="ProvisionVersion.ExtractedPositions"/>,
/// so adding a key never changes the schema either.
/// </summary>
public class SubQuestion
{
    public Guid Id { get; set; }

    public Guid ProvisionId { get; set; }
    public Provision? Provision { get; set; }

    /// <summary>
    /// Stable slug identifying this sub-question WITHIN its provision (e.g.
    /// "facility-scope"). Used as the key in the extracted-position vector, so
    /// it must be stable once assigned. Unique per provision.
    /// </summary>
    [Required, MaxLength(80)]
    public string Key { get; set; } = "";

    /// <summary>The question itself, in plain language.</summary>
    [Required, MaxLength(400)]
    public string Prompt { get; set; } = "";

    /// <summary>One line naming the real tradeoff this crux turns on (optional).</summary>
    [MaxLength(400)]
    public string? TradeoffDescription { get; set; }

    /// <summary>
    /// Known/expected discrete position labels for this sub-question (a hint to
    /// extraction; may be empty, and extraction may surface labels outside it).
    /// Stored as a Postgres text[].
    /// </summary>
    public string[] PositionOptions { get; set; } = Array.Empty<string>();

    public SubQuestionOrigin Origin { get; set; } = SubQuestionOrigin.Birth;

    /// <summary>For emergent sub-questions, the version that first introduced it.</summary>
    public Guid? IntroducedByVersionId { get; set; }

    public int OrderIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
