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

    /// <summary>
    /// Derive a supported local-news region from a US ZIP code, or <c>null</c>
    /// (national) when the ZIP falls outside a supported state. Uses the leading
    /// 3-digit ZIP prefix, which maps unambiguously to a state. Only the supported
    /// states are recognized; every other valid ZIP resolves to national.
    /// </summary>
    public static string? StateForZip(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return null;

        // Tolerate ZIP+4 ("98101-1234") and stray spaces by taking leading digits.
        var digits = new string(zip.Where(char.IsDigit).ToArray());
        if (digits.Length < 3 || !int.TryParse(digits[..3], out var prefix))
            return null;

        return prefix switch
        {
            >= 900 and <= 961 => California,  // 900xx–961xx
            >= 980 and <= 994 => Washington,  // 980xx–994xx
            >= 206 and <= 219 => Maryland,    // 206xx–219xx
            _ => null,
        };
    }
}
