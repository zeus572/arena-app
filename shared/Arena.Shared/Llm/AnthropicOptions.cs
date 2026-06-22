namespace Arena.Shared.Llm;

public class AnthropicOptions
{
    /// <summary>
    /// Master on/off switch for live LLM calls. When false the client behaves
    /// exactly as if no ApiKey were configured (callers fall back to heuristics),
    /// so you can pause API usage locally without deleting the key from secrets.
    /// Defaults to true so prod is unaffected.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string ApiKey { get; set; } = "";
    public string SonnetModel { get; set; } = "claude-sonnet-4-6";
    public string HaikuModel { get; set; } = "claude-haiku-4-5-20251001";
    public int DefaultMaxTokens { get; set; } = 4096;
}
