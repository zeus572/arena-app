namespace Civic.API.Models.DTOs;

public class BillTimelineStepDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Status { get; set; } = "";
    public int Order { get; set; }
}
