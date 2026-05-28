using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum BillStepStatus
{
    Done,
    Current,
    Upcoming,
}

public class BillTimelineStep
{
    public Guid Id { get; set; }

    [Required, MaxLength(40)]
    public string ExternalId { get; set; } = "";

    [Required, MaxLength(120)]
    public string Label { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(40)]
    public string Branch { get; set; } = "Legislative";

    public BillStepStatus Status { get; set; } = BillStepStatus.Upcoming;

    public int Order { get; set; }
}
