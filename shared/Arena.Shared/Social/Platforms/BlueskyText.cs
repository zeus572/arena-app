using System.Globalization;
using System.Text;

namespace Arena.Shared.Social.Platforms;

/// <summary>A Bluesky richtext facet: a UTF-8 byte range annotated as a link.</summary>
public sealed record BlueskyFacet(int ByteStart, int ByteEnd, string Uri);

/// <summary>
/// Deterministic, pure text helpers for the Bluesky adapter (SocialPublisher_Spec §4.1).
/// No network, no LLM. Bluesky counts length in graphemes and addresses link facets by UTF-8
/// byte range, so we must be grapheme-aware (not <c>string.Length</c>) and byte-aware (not char index).
/// </summary>
public static class BlueskyText
{
    /// <summary>Grapheme (text-element) count — the unit Bluesky uses for its 300 limit.</summary>
    public static int CountGraphemes(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var count = 0;
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext()) count++;
        return count;
    }

    public static bool ExceedsGraphemeLimit(string text, int maxGraphemes)
        => CountGraphemes(text) > maxGraphemes;

    /// <summary>
    /// Computes link facets by locating each link string in the text and converting char positions
    /// to UTF-8 byte offsets. Deterministic: links are matched left-to-right, each subsequent search
    /// continues past the previous match so repeated links get distinct facets.
    /// </summary>
    public static IReadOnlyList<BlueskyFacet> ComputeFacets(string text, IReadOnlyList<string> links)
    {
        var facets = new List<BlueskyFacet>();
        if (string.IsNullOrEmpty(text) || links.Count == 0) return facets;

        foreach (var link in links)
        {
            if (string.IsNullOrEmpty(link)) continue;
            var searchFrom = 0;
            while (true)
            {
                var idx = text.IndexOf(link, searchFrom, StringComparison.Ordinal);
                if (idx < 0) break;
                var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, idx));
                var byteLen = Encoding.UTF8.GetByteCount(link);
                facets.Add(new BlueskyFacet(byteStart, byteStart + byteLen, link));
                searchFrom = idx + link.Length;
            }
        }

        return facets.OrderBy(f => f.ByteStart).ToList();
    }
}
