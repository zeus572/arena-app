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

    /// <summary>Share (0..1) of people who answered correctly in the trailing 60 days.</summary>
    public double CorrectRate { get; set; }
    /// <summary>How many responses the 60-day moving average is based on.</summary>
    public int ResponseCount { get; set; }
}

/// <summary>Body for POST /api/quiz/questions/{id}/responses.</summary>
public class QuizResponseRequest
{
    public int SelectedIndex { get; set; }
}

/// <summary>Live poll result returned after a person answers a quiz question.</summary>
public class QuizPollResultDto
{
    public Guid QuestionId { get; set; }
    public int CorrectAnswerIndex { get; set; }
    public bool IsCorrect { get; set; }
    public int ResponseCount { get; set; }
    public int CorrectCount { get; set; }
    public double CorrectRate { get; set; }
    /// <summary>Window length in days for the moving average (informational).</summary>
    public int WindowDays { get; set; }
}
