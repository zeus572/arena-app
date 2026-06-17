using System.Text;

namespace Arena.API.Services;

/// <summary>
/// Hardens free-text that originates OUTSIDE our trust boundary — audience
/// questions, user-supplied debate topics/descriptions, and scraped
/// web/Wikipedia search results — before it is interpolated into an LLM prompt.
///
/// We deliberately do NOT try to classify text as "malicious": keyword-based
/// prompt-injection detection is unreliable and trivially bypassed. Instead we
/// make untrusted text safe to <em>embed</em> by:
///   1. stripping control characters that can be used to smuggle fake
///      conversational turns or terminal escapes,
///   2. neutralizing the fence token we rely on so a payload cannot forge a
///      closing fence and "break out" of the quoted region, and
///   3. capping length to bound any payload.
///
/// Callers then wrap the result with <see cref="WrapAsData"/> and add an
/// explicit instruction telling the model to treat the fenced block as data,
/// not as instructions. This follows the defense-in-depth guidance for
/// prompt injection: untrusted content goes in the user turn, clearly
/// delimited, and the model is told it is data.
/// </summary>
public static class PromptSanitizer
{
    // A sentinel that is extremely unlikely to occur in genuine input. Any
    // occurrence of the raw marker inside untrusted text is broken up so the
    // text cannot forge its own fence and escape the data region.
    private const string Fence = "<<<";

    /// <summary>
    /// Neutralizes a single untrusted string so it is safe to embed in a prompt.
    /// Returns an empty string for null/whitespace input.
    /// </summary>
    public static string Sanitize(string? input, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            // Keep newlines and tabs (legitimate formatting); drop other control
            // characters (NUL, carriage returns, escape sequences) that can be
            // used to fake role markers or smuggle terminal tricks.
            if (char.IsControl(ch) && ch != '\n' && ch != '\t') continue;
            sb.Append(ch);
        }

        var cleaned = sb.ToString();

        // Break up the fence token so untrusted text cannot forge a closing
        // fence and escape the data block it will be wrapped in.
        cleaned = cleaned.Replace(Fence, "< < <");

        if (cleaned.Length > maxLength)
            cleaned = cleaned[..maxLength].TrimEnd() + "…";

        return cleaned.Trim();
    }

    /// <summary>
    /// Wraps untrusted text in a clearly-labelled data fence after sanitizing it.
    /// The label describes what the block is (e.g. "AUDIENCE QUESTION"). The
    /// caller is responsible for adding the instruction that explains the block
    /// is data, not a command.
    /// </summary>
    public static string WrapAsData(string label, string? content, int maxLength = 2000)
    {
        var safe = Sanitize(content, maxLength);
        return $"{Fence}BEGIN {label}>>>\n{safe}\n{Fence}END {label}>>>";
    }
}
