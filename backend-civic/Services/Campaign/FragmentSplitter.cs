using System.Text.RegularExpressions;
using Civic.API.Models;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Splits a published post body into clause-level fragments (~5-15 word spans)
/// that users can react to. Pure and deterministic so the same body always
/// fragments identically. Boundaries are sentence and clause punctuation;
/// over-long runs are split again on the nearest word boundary.
/// </summary>
public static class FragmentSplitter
{
    // Break after sentence/clause terminators, keeping the terminator with the
    // preceding text: . ! ? ; : — , and en/em dashes used as clause breaks.
    private static readonly Regex ClauseBoundary =
        new(@"(?<=[\.!\?;:])\s+|\s+[—–-]\s+|(?<=,)\s+", RegexOptions.Compiled);

    private const int MaxWordsPerFragment = 15;
    private const int MaxCharsPerFragment = 120;

    public static List<PostFragment> Split(string body)
    {
        var fragments = new List<PostFragment>();
        if (string.IsNullOrWhiteSpace(body)) return fragments;

        var order = 0;
        var cursor = 0; // char offset into body we have consumed up to

        foreach (var rawPiece in SplitClauses(body))
        {
            var piece = rawPiece;
            if (string.IsNullOrWhiteSpace(piece)) continue;

            // Locate this piece in the body starting at the cursor so start/end
            // offsets are exact (handles repeated phrases correctly).
            var start = body.IndexOf(piece, cursor, StringComparison.Ordinal);
            if (start < 0) start = body.IndexOf(piece, StringComparison.Ordinal);
            if (start < 0) continue;

            var end = start + piece.Length;
            cursor = end;

            fragments.Add(new PostFragment
            {
                Text = piece,
                Start = start,
                End = end,
                Order = order++,
            });
        }

        // Fallback: if nothing split (e.g. one long run with no punctuation),
        // treat the whole trimmed body as a single fragment.
        if (fragments.Count == 0)
        {
            var trimmed = body.Trim();
            var start = body.IndexOf(trimmed, StringComparison.Ordinal);
            fragments.Add(new PostFragment
            {
                Text = trimmed,
                Start = Math.Max(0, start),
                End = Math.Max(0, start) + trimmed.Length,
                Order = 0,
            });
        }

        return fragments;
    }

    private static IEnumerable<string> SplitClauses(string body)
    {
        foreach (var clause in ClauseBoundary.Split(body))
        {
            var trimmed = clause.Trim();
            if (trimmed.Length == 0) continue;

            if (CountWords(trimmed) <= MaxWordsPerFragment && trimmed.Length <= MaxCharsPerFragment)
            {
                yield return trimmed;
                continue;
            }

            // Over-long clause: split into word-bounded chunks.
            foreach (var chunk in SplitOnWords(trimmed))
            {
                yield return chunk;
            }
        }
    }

    private static IEnumerable<string> SplitOnWords(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new List<string>();
        foreach (var w in words)
        {
            current.Add(w);
            var joined = string.Join(' ', current);
            if (current.Count >= MaxWordsPerFragment || joined.Length >= MaxCharsPerFragment)
            {
                yield return joined;
                current.Clear();
            }
        }
        if (current.Count > 0) yield return string.Join(' ', current);
    }

    private static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
