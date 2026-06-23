namespace Arena.API.Models;

/// <summary>
/// Why an address was added to the suppression list. Hard bounces and complaints
/// (spam reports) come from the ACS delivery-report webhook; Manual is for ops.
/// </summary>
public enum EmailSuppressionReason
{
    Bounce = 0,
    Complaint = 1,
    Manual = 2,
}

/// <summary>
/// An address we must never send transactional mail to again. Populated from ACS
/// Event Grid delivery reports (bounce/complaint) and checked before every send.
/// </summary>
public class EmailSuppression
{
    public Guid Id { get; set; }
    /// <summary>Normalized (trimmed, lower-cased) address.</summary>
    public string Email { get; set; } = string.Empty;
    public EmailSuppressionReason Reason { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
