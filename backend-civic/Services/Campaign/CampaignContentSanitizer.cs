using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Post-generation enforcement for campaign post bodies. Strips markdown,
/// collapses whitespace, caps emoji, and enforces the 160-char hard rule by
/// truncating at the last sentence boundary (then word boundary) when needed.
/// Pure functions — no I/O — so the enforcement rules are unit-testable.
/// </summary>
public static class CampaignContentSanitizer
{
    public const int MaxBodyLength = 160;
    private const int MaxEmoji = 2;

    private static readonly Regex MarkdownTokens =
        new(@"(\*\*|__|\*|_|`|~~|^#{1,6}\s+|^>\s+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LinkMarkup = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Cleans and length-enforces a raw model body. Returns the final body
    /// (always ≤ <see cref="MaxBodyLength"/>) and whether truncation occurred.
    /// </summary>
    public static (string Body, bool Truncated) Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("", false);

        // Markdown links -> link text; then strip remaining markdown tokens.
        var text = LinkMarkup.Replace(raw, "$1");
        text = MarkdownTokens.Replace(text, "");

        // Strip surrounding quotes the model sometimes adds.
        text = text.Trim().Trim('"', '“', '”').Trim();

        // Collapse whitespace/newlines into single spaces.
        text = Whitespace.Replace(text, " ").Trim();

        text = CapEmoji(text, MaxEmoji);

        if (text.Length <= MaxBodyLength) return (text, false);

        return (Truncate(text, MaxBodyLength), true);
    }

    /// <summary>Whether a raw body would need shortening (used to decide a re-prompt).</summary>
    public static bool ExceedsLimit(string raw) => Clean(raw).Truncated;

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;

        var window = text[..max];

        // Prefer the last sentence boundary inside the window, as long as it
        // keeps a meaningful chunk (avoids cutting to a 2-char "A.").
        const int minSentenceKeep = 20;
        var lastSentence = window.LastIndexOfAny(new[] { '.', '!', '?' });
        if (lastSentence + 1 >= minSentenceKeep)
        {
            return window[..(lastSentence + 1)].Trim();
        }

        // Otherwise cut at the last word boundary and add an ellipsis.
        var lastSpace = window.LastIndexOf(' ');
        var cut = lastSpace > 0 ? window[..lastSpace] : window;
        cut = cut.TrimEnd(',', ';', ':', '—', '–', '-', ' ');

        // Reserve room for the ellipsis within the limit.
        if (cut.Length > max - 1) cut = cut[..(max - 1)].TrimEnd();
        return cut + "…";
    }

    private static string CapEmoji(string text, int max)
    {
        var sb = new StringBuilder(text.Length);
        var count = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            if (IsEmoji(element))
            {
                if (count >= max) continue;
                count++;
            }
            sb.Append(element);
        }
        return Whitespace.Replace(sb.ToString(), " ").Trim();
    }

    private static bool IsEmoji(string element)
    {
        if (string.IsNullOrEmpty(element)) return false;
        var rune = char.ConvertToUtf32(element, 0);
        // Common emoji blocks: Misc Symbols & Pictographs, Emoticons, Transport,
        // Supplemental Symbols, Dingbats, Misc Symbols, flags.
        return rune is >= 0x1F300 and <= 0x1FAFF
            or >= 0x2600 and <= 0x27BF
            or >= 0x1F1E6 and <= 0x1F1FF;
    }
}
