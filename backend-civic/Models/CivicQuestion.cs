using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum CivicQuestionType
{
    SimplePairing,
    ForcedTradeoff,
    BudgetAllocation,
    PressureTest,
    Reflection,
    IssueSpecific,
}

public class CivicQuestion
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string ExternalId { get; set; } = "";

    public CivicQuestionType Type { get; set; }

    [Required]
    public string Prompt { get; set; } = "";

    public List<QuestionChoice> Choices { get; set; } = new();

    public int Order { get; set; }

    [MaxLength(80)]
    public string? Topic { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QuestionChoice
{
    [Required, MaxLength(8)]
    public string Key { get; set; } = "";

    [Required]
    public string Label { get; set; } = "";

    public List<AxisDelta> AxisDeltas { get; set; } = new();
}

public class AxisDelta
{
    [Required, MaxLength(60)]
    public string AxisKey { get; set; } = "";

    public double Delta { get; set; }
}
