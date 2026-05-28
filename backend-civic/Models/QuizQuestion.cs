using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public class QuizQuestion
{
    public Guid Id { get; set; }

    [Required, MaxLength(60)]
    public string ExternalId { get; set; } = "";

    [Required, MaxLength(120)]
    public string Topic { get; set; } = "";

    [Required, MaxLength(400)]
    public string Question { get; set; } = "";

    public string[] Options { get; set; } = Array.Empty<string>();

    public int CorrectAnswerIndex { get; set; }

    [Required, MaxLength(1000)]
    public string Explanation { get; set; } = "";

    [MaxLength(160)]
    public string? RelatedConceptSlug { get; set; }

    public int Order { get; set; }

    [MaxLength(20)]
    public string GenerationSource { get; set; } = CivicGenerationSource.Seed;

    public Guid? SourceNewsItemId { get; set; }
}
