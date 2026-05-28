using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum AnswerConfidence
{
    NotSure,
    SomewhatSure,
    VerySure,
}

public enum AnswerIntensity
{
    Low,
    Medium,
    High,
    NonNegotiable,
}

public class CivicAnswer
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid QuestionId { get; set; }
    public CivicQuestion? Question { get; set; }

    [Required, MaxLength(8)]
    public string SelectedChoiceKey { get; set; } = "";

    public AnswerConfidence Confidence { get; set; } = AnswerConfidence.SomewhatSure;
    public AnswerIntensity Intensity { get; set; } = AnswerIntensity.Medium;

    [MaxLength(120)]
    public string? ReasoningChoice { get; set; }

    [MaxLength(2000)]
    public string? FreeTextReasoning { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
