using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services.Email;

/// <summary>
/// Issues and consumes one-time <see cref="AccountToken"/>s for email verification
/// and password reset. Tokens are random, stored only as a hash, single-use, and
/// expire (verify 24h, reset 1h).
/// </summary>
public class AccountTokenService
{
    private static readonly TimeSpan VerifyLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private readonly ArenaDbContext _db;

    public AccountTokenService(ArenaDbContext db) => _db = db;

    /// <summary>Create a token for <paramref name="user"/>, invalidating any prior
    /// unused tokens of the same purpose. Returns the raw token (only ever exposed
    /// here, to be embedded in the email link).</summary>
    public async Task<string> IssueAsync(User user, AccountTokenPurpose purpose, CancellationToken ct = default)
    {
        // One live token per purpose: burn older outstanding ones so a leaked or
        // forgotten earlier link can't still be used.
        var outstanding = await _db.AccountTokens
            .Where(t => t.UserId == user.Id && t.Purpose == purpose && t.UsedAt == null)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var t in outstanding) t.UsedAt = now;

        var raw = TokenHasher.NewToken();
        _db.AccountTokens.Add(new AccountToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            Purpose = purpose,
            ExpiresAt = now + (purpose == AccountTokenPurpose.PasswordReset ? ResetLifetime : VerifyLifetime),
        });
        await _db.SaveChangesAsync(ct);
        return raw;
    }

    /// <summary>Validate a raw token for the given purpose and mark it used. Returns
    /// the owning user, or null if the token is unknown, wrong-purpose, expired or
    /// already used.</summary>
    public async Task<User?> ConsumeAsync(string rawToken, AccountTokenPurpose purpose, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = TokenHasher.Hash(rawToken.Trim());

        var token = await _db.AccountTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);

        if (token is null || token.UsedAt != null || token.ExpiresAt < DateTime.UtcNow)
            return null;

        token.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return token.User;
    }
}
