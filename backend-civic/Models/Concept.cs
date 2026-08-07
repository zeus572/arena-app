using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// The knowledge-item subtypes from PRD 04 §4.4. Existing rows are all
/// <see cref="Concept"/> — the default keeps them correct without a data migration.
/// </summary>
public enum KnowledgeKind
{
    Concept,
    Institution,
    Process,
    Law,
    Place,
    HistoricalEvent,
    EconomicMechanism,
    VocabularyTerm,
}

/// <summary>
/// A durable explainer — and, as of the Topic Rooms expansion, the PRD 04 §4.4 knowledge item.
///
/// This table is EXTENDED rather than forked into a parallel KnowledgeItem, because it
/// already carried Slug / Title / Category / PlainDefinition / WhyItMatters /
/// RelatedConcepts / CurrentExample / CommonMisunderstanding — roughly 90% of what the PRD
/// asks for. A second table would have immediately violated the platform's own acceptance
/// criterion ("one fact can be reused across multiple pages without duplication") and
/// forked /api/concepts on day one.
/// </summary>
public class Concept
{
    public Guid Id { get; set; }

    [Required, MaxLength(160)]
    public string Slug { get; set; } = "";

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required, MaxLength(64)]
    public string Category { get; set; } = "";

    [Required]
    public string PlainDefinition { get; set; } = "";

    [Required]
    public string WhyItMatters { get; set; } = "";

    public string[] WhereYouSeeIt { get; set; } = Array.Empty<string>();

    [Required]
    public string CurrentExample { get; set; } = "";

    [Required]
    public string CommonMisunderstanding { get; set; } = "";

    public string[] RelatedConcepts { get; set; } = Array.Empty<string>();

    [Required]
    public string TryItQuestion { get; set; } = "";

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public Guid? SourceNewsItemId { get; set; }

    // --- Topic Rooms knowledge-item fields ---------------------------------------------

    public KnowledgeKind KnowledgeKind { get; set; } = KnowledgeKind.Concept;

    /// <summary>
    /// A one-line gloss for the design 1h glossary grid ("Words you will hit today").
    /// <see cref="PlainDefinition"/> is long-form and would blow the two-column layout.
    /// </summary>
    [MaxLength(300)]
    public string? ShortGloss { get; set; }

    /// <summary>
    /// The concept this one is easy to confuse with (design 1h's "Easy to confuse" pairs).
    /// Stored as a slug rather than an FK so a seed file can name a pair in either order
    /// without a resolution pass.
    /// </summary>
    [MaxLength(160)]
    public string? ConfusionPairSlug { get; set; }

    /// <summary>The one sentence that tells the pair apart. Useless without the pair.</summary>
    [MaxLength(500)]
    public string? ConfusionDiscriminator { get; set; }
}
