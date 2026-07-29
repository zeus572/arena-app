using System.Security.Cryptography;
using System.Text;

namespace Arena.API.Services.Email;

/// <summary>
/// Constant-time comparison for the shared secret guarding the operator email
/// endpoints (<c>/api/admin/email-smoke</c>, <c>/api/admin/email/resend-verification</c>).
/// Hashes both sides first so the compare is fixed-length regardless of input.
/// </summary>
public static class SharedSecret
{
    public static bool Matches(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
