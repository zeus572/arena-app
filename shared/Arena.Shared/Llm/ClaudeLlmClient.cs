using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arena.Shared.Llm;

/// <summary>
/// Thin HTTP wrapper over the Anthropic Messages API for structured JSON
/// generation. Intentionally bare-bones — no tool use, no streaming, no
/// conversation memory. Use it when you have a deterministic system + user
/// prompt that should yield a single JSON object/array.
/// </summary>
public class ClaudeLlmClient : ILlmClient
{
    private const string MessagesPath = "v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly AnthropicOptions _opts;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public ClaudeLlmClient(
        HttpClient http,
        IOptions<AnthropicOptions> opts,
        ILogger<ClaudeLlmClient>? logger = null)
    {
        _http = http;
        _opts = opts.Value;
        _logger = logger ?? NullLogger<ClaudeLlmClient>.Instance;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.anthropic.com/");
        }
        // Header values are configured per-request below so a swapped-in
        // mock HttpClient in tests doesn't need to set them up.
    }

    public async Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled)
        {
            throw new LlmException("Anthropic LLM is disabled (Anthropic:Enabled=false).");
        }

        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            throw new LlmException("Anthropic:ApiKey not configured.");
        }

        var model = tier == LlmModelTier.Haiku ? _opts.HaikuModel : _opts.SonnetModel;
        var raw = await CallAnthropicAsync(model, systemPrompt, userPrompt, maxTokens, ct);
        try
        {
            return ParseJson<T>(raw);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Claude returned non-JSON; retrying with a stricter reminder.");
            var rawRetry = await CallAnthropicAsync(
                model,
                systemPrompt + "\n\nIMPORTANT: respond with ONLY a single JSON value. No prose, no markdown fences.",
                userPrompt,
                maxTokens,
                ct);
            try
            {
                return ParseJson<T>(rawRetry);
            }
            catch (JsonException jex)
            {
                // The call itself succeeded (HTTP 200) — the model just wouldn't emit the
                // requested JSON, even after a stricter reminder. That's a per-request content
                // failure (a prose preamble/refusal, or truncation), NOT an API outage, so it
                // must be tagged BadResponse: batch callers fail just this item and keep going
                // instead of halting and re-queuing a poison pill at the head of the batch.
                // Log a snippet of what Claude actually said so triage doesn't need the DB.
                _logger.LogWarning(
                    "Claude returned non-JSON after retry; first {N} chars: {Snippet}",
                    120, Snippet(rawRetry, 120));
                throw new LlmException(
                    "Claude returned non-JSON after retry.",
                    rawResponse: rawRetry,
                    inner: jex,
                    kind: LlmFailureKind.BadResponse);
            }
        }
    }

    private async Task<string> CallAnthropicAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        int? maxTokens,
        CancellationToken ct)
    {
        var body = new
        {
            model,
            max_tokens = maxTokens ?? _opts.DefaultMaxTokens,
            // Cache the system prompt with an ephemeral breakpoint. Most callers reuse
            // the same system prompt across many requests (judges, framings, campaign
            // posts), so repeated prefixes bill at cache-read rates instead of full
            // input. Below the model's minimum cacheable prefix it simply won't cache.
            system = new object[]
            {
                new
                {
                    type = "text",
                    text = systemPrompt,
                    cache_control = new { type = "ephemeral" },
                },
            },
            messages = new[]
            {
                new { role = "user", content = userPrompt },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add("x-api-key", _opts.ApiKey);
        req.Headers.Add("anthropic-version", AnthropicVersion);

        using var resp = await _http.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new LlmException(
                $"Anthropic API returned {(int)resp.StatusCode} {resp.ReasonPhrase}.",
                rawResponse: respBody,
                kind: LlmFailureKind.CallFailed);
        }

        return ExtractText(respBody);
    }

    private static string ExtractText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            throw new LlmException(
                "Anthropic response missing 'content' array.",
                rawResponse: responseBody,
                kind: LlmFailureKind.CallFailed);
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) &&
                t.GetString() == "text" &&
                block.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? "";
            }
        }
        throw new LlmException(
            "Anthropic response contained no text block.",
            rawResponse: responseBody,
            kind: LlmFailureKind.CallFailed);
    }

    private static T ParseJson<T>(string text)
    {
        var json = ExtractJson(text);
        var parsed = JsonSerializer.Deserialize<T>(json, JsonOpts);
        if (parsed is null)
        {
            throw new JsonException("Deserialized value was null.");
        }
        return parsed;
    }

    /// <summary>
    /// Pull the JSON value out of a model response that may wrap it in markdown fences
    /// (```json … ```) or surrounding prose ("Here's the JSON: {…}. Hope that helps!").
    /// The model is asked for bare JSON, but it occasionally adds a preamble anyway; rather
    /// than fail the whole request we salvage the first balanced object/array. Genuine
    /// no-JSON responses (a refusal starting with prose) leave nothing to slice, so parsing
    /// still throws and the caller's retry / BadResponse path takes over.
    /// </summary>
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

    private static string Snippet(string s, int max)
    {
        var oneLine = s.Trim().ReplaceLineEndings(" ");
        return oneLine.Length <= max ? oneLine : oneLine[..max] + "…";
    }
}
