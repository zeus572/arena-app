using System.Security.Cryptography;
using System.Text;

namespace Arena.Shared.Security;

/// <summary>
/// Constant-time comparison for the shared secrets guarding operator-only endpoints
/// (email smoke test, daily-stats, daily-report trigger) in both backends. Hashes
/// both sides first so the compare is fixed-length regardless of input length.
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
