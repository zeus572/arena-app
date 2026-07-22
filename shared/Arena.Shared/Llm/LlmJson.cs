using System.Text.Json;

namespace Arena.Shared.Llm;

/// <summary>
/// Provider-agnostic salvage + deserialize for structured-JSON LLM responses.
/// Models are asked for bare JSON but occasionally wrap it in markdown fences
/// (```json … ```) or surrounding prose ("Here's the JSON: {…}. Hope that helps!").
/// These helpers pull the first balanced JSON value out of such a payload so a
/// stray preamble doesn't fail the whole request. Shared by every
/// <see cref="StructuredJsonLlmClient"/> so Claude and GPT behave identically.
/// </summary>
internal static class LlmJson
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Deserialize the first balanced JSON value found in <paramref name="text"/> as
    /// <typeparamref name="T"/>. Throws <see cref="JsonException"/> when nothing parseable
    /// is present (a prose refusal) or the value is null — the caller's retry / BadResponse
    /// path takes over from there.
    /// </summary>
    public static T Parse<T>(string text)
    {
        var json = ExtractJson(text);
        var parsed = JsonSerializer.Deserialize<T>(json, JsonOpts);
        if (parsed is null)
        {
            throw new JsonException("Deserialized value was null.");
        }
        return parsed;
    }

    private static string ExtractJson(string text)
    {
        var s = text.Trim();

        // 1) If the payload is fenced, take the fence body first.
        var fence = s.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var bodyStart = s.IndexOf('\n', fence);
            if (bodyStart >= 0)
            {
                var bodyEnd = s.IndexOf("```", bodyStart, StringComparison.Ordinal);
                s = (bodyEnd > bodyStart ? s[(bodyStart + 1)..bodyEnd] : s[(bodyStart + 1)..]).Trim();
            }
        }

        // 2) Slice out the first balanced { … } / [ … ] so leading prose ("I'll analyze…")
        //    or a trailing sign-off doesn't derail deserialization. Returns the whole string
        //    untouched if there's no brace to anchor on (Deserialize will then throw cleanly).
        return SliceFirstJsonValue(s) ?? s;
    }

    private static string? SliceFirstJsonValue(string s)
    {
        var open = s.IndexOfAny(new[] { '{', '[' });
        if (open < 0) return null;

        var openChar = s[open];
        var closeChar = openChar == '{' ? '}' : ']';
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = open; i < s.Length; i++)
        {
            var c = s[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == openChar) depth++;
            else if (c == closeChar && --depth == 0) return s[open..(i + 1)];
        }

        return null; // unbalanced — likely truncated at max_tokens
    }

    public static string Snippet(string s, int max)
    {
        var oneLine = s.Trim().ReplaceLineEndings(" ");
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }
}
