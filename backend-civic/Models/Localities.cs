namespace Civic.API.Models;

/// <summary>
/// The set of supported local-news regions. A locality is a 2-letter US state
/// code; <c>null</c>/empty means "national" (visible to everyone). This single
/// allowlist keeps the profile dropdown, the <c>News:LocalSources</c> config
/// keys, and read-scoping validation in sync.
/// </summary>
public static class Localities
{
    public const string Washington = "WA";
    public const string Maryland = "MD";
    public const string California = "CA";

    /// <summary>All supported local state codes (national is represented by null).</summary>
    public static readonly IReadOnlyList<string> Supported = new[]
    {
        Washington, Maryland, California,
    };

    /// <summary>
    /// Normalize raw input to a supported state code, or <c>null</c> for national.
    /// Returns false when a non-empty value isn't in the allowlist.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? locality)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            locality = null;
            return true;
        }

        var code = raw.Trim().ToUpperInvariant();
        foreach (var s in Supported)
        {
            if (string.Equals(s, code, StringComparison.OrdinalIgnoreCase))
            {
                locality = s;
                return true;
            }
        }

        locality = null;
        return false;
    }
}
