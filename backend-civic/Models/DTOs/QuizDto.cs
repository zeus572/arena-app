namespace Civic.API.Models.DTOs;

public class QuizQuestionDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Question { get; set; } = "";
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectAnswerIndex { get; set; }
    public string Explanation { get; set; } = "";
    public string? RelatedConceptSlug { get; set; }
    public int Order { get; set; }
}
