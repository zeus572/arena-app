namespace Civic.API.Models.DTOs;

public class QuestionChoiceDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public class QuestionDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string? Topic { get; set; }
    public int Order { get; set; }
    public List<QuestionChoiceDto> Choices { get; set; } = new();
}
