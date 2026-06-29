using System.Text.RegularExpressions;

namespace Arena.Shared.Social;

/// <summary>Deterministic http/https URL extraction from post text (for Bluesky facets). No LLM.</summary>
public static partial class LinkExtractor
{
    [GeneratedRegex(@"https?://[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        return UrlRegex().Matches(text)
            .Select(m => m.Value.TrimEnd('.', ',', ')', ']'))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
