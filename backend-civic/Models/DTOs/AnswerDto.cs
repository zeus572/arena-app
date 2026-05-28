using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models.DTOs;

public class CreateAnswerRequest
{
    [Required]
    public Guid QuestionId { get; set; }

    [Required, MaxLength(8)]
    public string SelectedChoiceKey { get; set; } = "";

    [Required, MaxLength(20)]
    public string Confidence { get; set; } = "SomewhatSure";

    [Required, MaxLength(20)]
    public string Intensity { get; set; } = "Medium";

    [MaxLength(120)]
    public string? ReasoningChoice { get; set; }

    [MaxLength(2000)]
    public string? FreeTextReasoning { get; set; }
}

public class AnswerDto
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionExternalId { get; set; } = "";
    public string SelectedChoiceKey { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string Intensity { get; set; } = "";
    public string? ReasoningChoice { get; set; }
    public string? FreeTextReasoning { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
