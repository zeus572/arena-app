namespace Arena.Shared.Llm;

public class AnthropicOptions
{
    public string ApiKey { get; set; } = "";
    public string SonnetModel { get; set; } = "claude-sonnet-4-6";
    public string HaikuModel { get; set; } = "claude-haiku-4-5-20251001";
    public int DefaultMaxTokens { get; set; } = 4096;
}
