using System.Text.Json;
using Arena.Shared.Llm;

namespace Civic.ApiTests.Fakes;

/// <summary>
/// Models a MID-GENERATION live Anthropic outage: serves canned JSON for the first few
/// registered types (so generation gets partway — e.g. past the relevance gate and the
/// briefing), then throws <see cref="LlmFailureKind.CallFailed"/> for everything else
/// (e.g. out of credits between calls). Used to prove the generator discards partial work
/// and requeues the item instead of persisting an orphan "half-story".
/// </summary>
public sealed class OutageLlmClient : ILlmClient
{
    private readonly Dictionary<string, string> _canned;

    public OutageLlmClient(Dictionary<string, string> cannedByTypeName) => _canned = cannedByTypeName;

    public Task<T> GenerateStructuredAsync<T>(
        string systemPrompt, string userPrompt, LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null, CancellationToken ct = default)
    {
        if (_canned.TryGetValue(typeof(T).Name, out var json))
        {
            var parsed = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return Task.FromResult(parsed);
        }
        throw new LlmException("Anthropic API returned 429 Too Many Requests.", kind: LlmFailureKind.CallFailed);
    }
}
