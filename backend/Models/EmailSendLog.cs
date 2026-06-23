namespace Arena.API.Models;

/// <summary>
/// One row per account email we actually dispatch. Backs durable per-address rate
/// limiting (so a restart can't reset the counter) and gives an audit trail of
/// what was sent where.
/// </summary>
public class EmailSendLog
{
    public Guid Id { get; set; }
    /// <summary>Normalized (trimmed, lower-cased) recipient address.</summary>
    public string Email { get; set; } = string.Empty;
    public AccountTokenPurpose Purpose { get; set; }
    public string? IpAddress { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
