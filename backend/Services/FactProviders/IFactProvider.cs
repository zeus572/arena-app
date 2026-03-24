namespace Arena.API.Services.FactProviders;

public class FactResult
{
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public interface IFactProvider
{
    string Name { get; }
    Task<List<FactResult>> SearchAsync(string query, int maxResults = 3);
}
