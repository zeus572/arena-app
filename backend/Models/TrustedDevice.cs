namespace Arena.API.Models;

/// <summary>
/// A device the user chose to "remember" so it can skip the TOTP step at login for
/// a bounded window (default 90 days). Only the SHA-256 hash of the raw token is
/// stored — the raw value lives in the client's local storage and is presented at
/// login. Revoked on disable-MFA and on password reset.
/// </summary>
public class TrustedDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
