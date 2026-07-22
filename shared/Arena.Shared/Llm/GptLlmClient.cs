using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arena.Shared.Llm;

/// <summary>
/// Thin HTTP wrapper over the OpenAI Chat Completions API, shaped as a drop-in
/// backup for <see cref="ClaudeLlmClient"/>. Same structured-JSON contract, same
/// tier semantics — the Sonnet tier maps to GPT-5.6 Terra and the Haiku tier to
/// GPT-5.6 Luna (see <see cref="OpenAiOptions"/>). No tool use, no streaming: one
/// system + user prompt in, one JSON value out. OpenAI caches repeated prompt
/// prefixes automatically (no cache_control field needed), so the reused system
/// prompts bill at cache-read rates without any extra plumbing.
/// </summary>
public class GptLlmClient : StructuredJsonLlmClient
{
    private const string ChatCompletionsPath = "v1/chat/completions";

    private readonly HttpClient _http;
    private readonly OpenAiOptions _opts;

    public GptLlmClient(
        HttpClient http,
        IOptions<OpenAiOptions> opts,
        ILogger<GptLlmClient>? logger = null)
        : base(logger ?? NullLogger<GptLlmClient>.Instance)
    {
        _http = http;
        _opts = opts.Value;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.openai.com/");
        }
    }

    protected override string ProviderName => "GPT";

    protected override string? UnavailableReason()
    {
        if (!_opts.Enabled)
        {
            return "OpenAI LLM is disabled (OpenAI:Enabled=false).";
        }
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            return "OpenAI:ApiKey not configured.";
        }
        return null;
    }

    protected override string ResolveModel(LlmModelTier tier) =>
        tier == LlmModelTier.Haiku ? _opts.HaikuBackupModel : _opts.SonnetBackupModel;

    protected override async Task<string> CallAsync(
        string model,
        string systemPrompt,
        string userPrompt,
        int? maxTokens,
        CancellationToken ct)
    {
        var body = new
        {
            model,
            // GPT-5.x models take max_completion_tokens (max_tokens is rejected).
            max_completion_tokens = maxTokens ?? _opts.DefaultMaxTokens,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsPath)
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        using var resp = await _http.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new LlmException(
                $"OpenAI API returned {(int)resp.StatusCode} {resp.ReasonPhrase}.",
                rawResponse: respBody,
                kind: LlmFailureKind.CallFailed);
        }

        return ExtractText(respBody);
    }

    private static string ExtractText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new LlmException(
                "OpenAI response missing 'choices' array.",
                rawResponse: responseBody,
                kind: LlmFailureKind.CallFailed);
        }

        var first = choices[0];
        if (first.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? "";
        }

        throw new LlmException(
            "OpenAI response contained no message content.",
            rawResponse: responseBody,
            kind: LlmFailureKind.CallFailed);
    }
}
