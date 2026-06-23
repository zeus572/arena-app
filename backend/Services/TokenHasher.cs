using System.Security.Cryptography;
using System.Text;

namespace Arena.API.Services;

/// <summary>
/// Shared helpers for opaque secret tokens (refresh tokens, email verification,
/// password reset). Raw tokens are cryptographically random; only their SHA-256
/// hash is ever stored, so a database leak can't be replayed against the user.
/// </summary>
public static class TokenHasher
{
    /// <summary>Generate a new random URL-safe hex token.</summary>
    public static string NewToken(int byteLength = 32) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));

    /// <summary>SHA-256 hash of a token, as upper-case hex (matches storage format).</summary>
    public static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
