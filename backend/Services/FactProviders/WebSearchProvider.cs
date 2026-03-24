using System.Net;
using HtmlAgilityPack;

namespace Arena.API.Services.FactProviders;

/// <summary>
/// General web search provider using DuckDuckGo's HTML interface (no API key required).
/// </summary>
public class WebSearchProvider : IFactProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<WebSearchProvider> _logger;

    public string Name => "WebSearch";

    public WebSearchProvider(HttpClient http, ILogger<WebSearchProvider> logger)
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
            var url = $"https://html.duckduckgo.com/html/?q={encoded}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return results;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'result')]");
            if (resultNodes is null) return results;

            foreach (var node in resultNodes.Take(maxResults))
            {
                var linkNode = node.SelectSingleNode(".//a[contains(@class,'result__a')]");
                var snippetNode = node.SelectSingleNode(".//a[contains(@class,'result__snippet')]");

                if (linkNode is null) continue;

                var title = linkNode.InnerText?.Trim() ?? "";
                var href = linkNode.GetAttributeValue("href", "");
                var snippet = snippetNode?.InnerText?.Trim() ?? "";

                if (string.IsNullOrEmpty(title)) continue;

                results.Add(new FactResult
                {
                    Source = "Web",
                    Title = title,
                    Content = snippet,
                    Url = href,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web search failed for query: {Query}", query);
        }

        return results;
    }
}
