namespace Civic.API.Models.DTOs;

public class ElectionDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public string? Region { get; set; }
    public string? Description { get; set; }
}
