namespace Arena.Shared.Llm;

public class LlmException : Exception
{
    public string? RawResponse { get; }

    public LlmException(string message, string? rawResponse = null, Exception? inner = null)
        : base(message, inner)
    {
        RawResponse = rawResponse;
    }
}
