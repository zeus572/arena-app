using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

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
}
