namespace Arena.API.Models.DTOs;

public class CreateDebateRequest
{
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Format { get; set; }
    public Guid? ProponentId { get; set; }
    public Guid? OpponentId { get; set; }
}
