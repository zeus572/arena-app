using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Civic.API.Services.Generation;

internal static class Slugify
{
    private static readonly Regex InvalidChars = new("[^a-z0-9]+", RegexOptions.Compiled);

    public static string From(string input, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(input)) return "untitled";

        var stripped = RemoveDiacritics(input).ToLowerInvariant();
        var slug = InvalidChars.Replace(stripped, "-").Trim('-');
        if (slug.Length > maxLength) slug = slug[..maxLength].TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? "untitled" : slug;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
