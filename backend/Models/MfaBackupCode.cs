namespace Arena.API.Models;

/// <summary>
/// A single-use recovery code accepted in place of a TOTP code when the user has
/// lost access to their authenticator. Only the SHA-256 hash of the raw code is
/// persisted — the plaintext is shown to the user exactly once, at generation.
/// </summary>
public class MfaBackupCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
