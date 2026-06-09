using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

/// <summary>
/// One person's answer to one quiz question. These power the "global poll" view: the
/// share of people who got a question right, computed as a trailing-60-day moving average.
/// </summary>
public class QuizResponse
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }
    public QuizQuestion? Question { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public int SelectedIndex { get; set; }

    public bool IsCorrect { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
