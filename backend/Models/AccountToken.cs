namespace Arena.API.Models;

/// <summary>
/// Purpose of a one-time <see cref="AccountToken"/>. A token is only ever valid
/// for the single purpose it was issued for.
/// </summary>
public enum AccountTokenPurpose
{
    EmailVerify = 0,
    PasswordReset = 1,

    /// <summary>
    /// Not an account token — no <see cref="AccountToken"/> row ever carries this. It exists
    /// only to label the operator's daily engagement email in <see cref="EmailSendLog"/>, which
    /// reuses this enum as its purpose column. Stored as an int, so adding it needs no
    /// migration. Excluded from the account-email rate limit, which is about protecting users'
    /// inboxes, not the operator's.
    /// </summary>
    DailyReport = 2,
}

/// <summary>
/// A single-use, expiring secret used for email verification and password reset.
/// Only the SHA-256 hash of the raw token is persisted — the raw value lives only
/// in the email link we send the user, never in the database.
/// </summary>
public class AccountToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public AccountTokenPurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
