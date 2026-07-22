namespace Arena.Shared.Llm;

/// <summary>
/// Configuration for the OpenAI GPT backup used by <see cref="FallbackLlmClient"/>
/// when the primary Claude client fails or is rate-limited. Mirrors
/// <see cref="AnthropicOptions"/> so the two providers are interchangeable.
/// </summary>
public class OpenAiOptions
{
    /// <summary>
    /// Master on/off switch for live OpenAI calls, symmetric with
    /// <see cref="AnthropicOptions.Enabled"/>. When false the client is Unavailable
    /// (callers fall back to heuristics). To pause ALL LLM spend on a dev box, turn
    /// this AND Anthropic:Enabled off. Defaults to true so prod backup is armed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Backup for the Claude <b>Sonnet</b> tier (balanced content generation).
    /// GPT-5.6 Terra is OpenAI's balanced mid-tier — same role, ~same price
    /// ($2.50/$15 vs Sonnet 4.6's $3/$15) and ~1M context. Pin the explicit id:
    /// the bare "gpt-5.6" alias routes to the flagship Sol tier.
    /// </summary>
    public string SonnetBackupModel { get; set; } = "gpt-5.6-terra";

    /// <summary>
    /// Backup for the Claude <b>Haiku</b> tier (fast/cheap judges and small decisions).
    /// GPT-5.6 Luna is OpenAI's fast tier — same role, ~same price ($1/$6 vs
    /// Haiku 4.5's $1/$5).
    /// </summary>
    public string HaikuBackupModel { get; set; } = "gpt-5.6-luna";

    public int DefaultMaxTokens { get; set; } = 4096;
}
