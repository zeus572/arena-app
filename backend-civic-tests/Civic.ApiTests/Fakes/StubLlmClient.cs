using System.Collections.Concurrent;
using System.Text.Json;
using Arena.Shared.Llm;

namespace Civic.ApiTests.Fakes;

/// <summary>
/// In-memory <see cref="ILlmClient"/> for tests. Routes by target-type name —
/// the SUT just asks for <c>GenerateStructuredAsync&lt;GeneratedBriefingDto&gt;</c>
/// and the fake looks up "GeneratedBriefingDto" in <see cref="Responses"/>.
///
/// Use the test fixture <see cref="WithJson(string,string)"/> helper to
/// register canned JSON per type. Anything not registered throws.
/// </summary>
public class StubLlmClient : ILlmClient
{
    public ConcurrentDictionary<string, string> Responses { get; } = new();
    public List<(string Type, LlmModelTier Tier, string SystemPrompt, string UserPrompt)> Calls { get; } = new();

    public StubLlmClient WithJson<T>(string json)
    {
        Responses[typeof(T).Name] = json;
        return this;
    }

    public StubLlmClient WithJson(string typeName, string json)
    {
        Responses[typeName] = json;
        return this;
    }

    public Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        Calls.Add((typeof(T).Name, tier, systemPrompt, userPrompt));

        if (!Responses.TryGetValue(typeof(T).Name, out var json))
        {
            throw new InvalidOperationException(
                $"StubLlmClient has no response registered for {typeof(T).Name}. " +
                $"Available: {string.Join(", ", Responses.Keys)}");
        }

        var parsed = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (parsed is null) throw new InvalidOperationException("Stub deserialized to null");
        return Task.FromResult(parsed);
    }
}
