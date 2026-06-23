namespace Civic.API.Models;

/// <summary>
/// The set of supported self-reported age brackets, collected at sign-up for
/// personalization. Stored as a stable key (e.g. "25_34"); the human label lives
/// on the client. This single allowlist keeps the sign-up form and server-side
/// validation in sync. Keep in sync with the frontend <c>AGE_RANGES</c> constant.
/// </summary>
public static class AgeRanges
{
    /// <summary>All supported age-bracket keys, youngest first.</summary>
    public static readonly IReadOnlyList<string> Supported = new[]
    {
        "under_18", "18_24", "25_34", "35_44", "45_54", "55_64", "65_plus",
    };

    /// <summary>
    /// Normalize raw input to a supported age-bracket key, or <c>null</c> when the
    /// value is empty. Returns false when a non-empty value isn't in the allowlist.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? ageRange)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            ageRange = null;
            return true;
        }

        var key = raw.Trim().ToLowerInvariant();
        foreach (var r in Supported)
        {
            if (string.Equals(r, key, StringComparison.OrdinalIgnoreCase))
            {
                ageRange = r;
                return true;
            }
        }

        ageRange = null;
        return false;
    }
}
