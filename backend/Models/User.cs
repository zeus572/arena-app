namespace Arena.API.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public string? PoliticalLeaning { get; set; }
    public string? AvatarUrl { get; set; }
    public UserPlan Plan { get; set; } = UserPlan.Free;
    public string? AuthProvider { get; set; }
    public string? ExternalId { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsAnonymous { get; set; }
    public int Xp { get; set; }

    // TOTP two-factor authentication (opt-in). When MfaEnabled is true, login
    // requires a second factor. TotpSecretEnc holds the AES-GCM-encrypted base32
    // secret — it is set during setup (before enable) and stays set while enabled.
    public bool MfaEnabled { get; set; }
    public string? TotpSecretEnc { get; set; }
    public DateTime? MfaEnrolledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
