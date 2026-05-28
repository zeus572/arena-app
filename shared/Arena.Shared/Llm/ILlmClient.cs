namespace Arena.Shared.Llm;

public enum LlmModelTier
{
    /// <summary>Cheap, fast — use for boolean judges and small decisions.</summary>
    Haiku,
    /// <summary>Default — use for content generation.</summary>
    Sonnet,
}

public interface ILlmClient
{
    /// <summary>
    /// Calls the configured Anthropic Messages endpoint with a system + user
    /// prompt, parses the assistant response as JSON and deserializes it as
    /// <typeparamref name="T"/>. Retries once on JSON parse failure with an
    /// "answer ONLY in JSON" reminder.
    /// </summary>
    /// <exception cref="LlmException">
    /// Thrown when the API returns non-2xx, when the model returns content
    /// that can't be parsed as JSON after the retry, or when no API key is
    /// configured.
    /// </exception>
    Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        LlmModelTier tier = LlmModelTier.Sonnet,
        int? maxTokens = null,
        CancellationToken ct = default);
}
