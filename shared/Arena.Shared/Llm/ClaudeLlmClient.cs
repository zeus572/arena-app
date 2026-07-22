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
/// prompt that should yield a single JSON object/array. The retry/salvage
/// policy lives in <see cref="StructuredJsonLlmClient"/>; this class only knows
/// how to talk to Anthropic.
/// </summary>
public class ClaudeLlmClient : StructuredJsonLlmClient
{
    private const string MessagesPath = "v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly AnthropicOptions _opts;

    public ClaudeLlmClient(
        HttpClient http,
        IOptions<AnthropicOptions> opts,
        ILogger<ClaudeLlmClient>? logger = null)
        : base(logger ?? NullLogger<ClaudeLlmClient>.Instance)
    {
        _http = http;
        _opts = opts.Value;

        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri("https://api.anthropic.com/");
        }
        // Header values are configured per-request below so a swapped-in
        // mock HttpClient in tests doesn't need to set them up.
    }

    protected override string ProviderName => "Claude";

    protected override string? UnavailableReason()
    {
        if (!_opts.Enabled)
        {
            return "Anthropic LLM is disabled (Anthropic:Enabled=false).";
        }
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            return "Anthropic:ApiKey not configured.";
        }
        return null;
    }

    protected override string ResolveModel(LlmModelTier tier) =>
        tier == LlmModelTier.Haiku ? _opts.HaikuModel : _opts.SonnetModel;

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
}
