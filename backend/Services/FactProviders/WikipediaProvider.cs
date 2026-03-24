using System.Net;
using System.Text.Json;

namespace Arena.API.Services.FactProviders;

public class WikipediaProvider : IFactProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<WikipediaProvider> _logger;

    public string Name => "Wikipedia";

    public WikipediaProvider(HttpClient http, ILogger<WikipediaProvider> logger)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ArenaBot/1.0)");
        _logger = logger;
    }

    public async Task<List<FactResult>> SearchAsync(string query, int maxResults = 3)
    {
        var results = new List<FactResult>();

        try
        {
            var encoded = WebUtility.UrlEncode(query);
            var url = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={encoded}&srlimit={maxResults}&format=json&utf8=1";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var searchResults = doc.RootElement
                .GetProperty("query")
                .GetProperty("search");

            foreach (var item in searchResults.EnumerateArray())
            {
                var title = item.GetProperty("title").GetString() ?? "";
                var snippet = item.GetProperty("snippet").GetString() ?? "";
                // Strip HTML from snippet
                snippet = System.Text.RegularExpressions.Regex.Replace(snippet, "<.*?>", "");

                results.Add(new FactResult
                {
                    Source = "Wikipedia",
                    Title = title,
                    Content = snippet,
                    Url = $"https://en.wikipedia.org/wiki/{WebUtility.UrlEncode(title.Replace(' ', '_'))}",
                });
            }

            // Fetch the extract for the top result
            if (results.Count > 0)
            {
                await EnrichWithExtractAsync(results[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wikipedia search failed for query: {Query}", query);
        }

        return results;
    }

    private async Task EnrichWithExtractAsync(FactResult result)
    {
        try
        {
            var encoded = WebUtility.UrlEncode(result.Title);
            var url = $"https://en.wikipedia.org/w/api.php?action=query&titles={encoded}&prop=extracts&exintro=1&explaintext=1&format=json&utf8=1";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out var extract))
                {
                    var text = extract.GetString() ?? "";
                    result.Content = text.Length > 800 ? text[..800] : text;
                }
            }
        }
        catch
        {
            // silently fail
        }
    }
}
