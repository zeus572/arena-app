using System.Net;
using HtmlAgilityPack;

namespace Arena.API.Services.FactProviders;

public class UsaFactsProvider : IFactProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<UsaFactsProvider> _logger;

    public string Name => "USAFacts";

    public UsaFactsProvider(HttpClient http, ILogger<UsaFactsProvider> logger)
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
            var url = $"https://usafacts.org/search/?q={encoded}";

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("USAFacts search returned {Status}", response.StatusCode);
                return results;
            }

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract search result items
            var resultNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, '/articles/') or contains(@href, '/data/')]");
            if (resultNodes is null) return results;

            foreach (var node in resultNodes.Take(maxResults))
            {
                var href = node.GetAttributeValue("href", "");
                var title = node.InnerText?.Trim();

                if (string.IsNullOrEmpty(title) || title.Length < 10) continue;

                var fullUrl = href.StartsWith("http") ? href : $"https://usafacts.org{href}";

                results.Add(new FactResult
                {
                    Source = "USAFacts",
                    Title = title.Length > 200 ? title[..200] : title,
                    Content = title,
                    Url = fullUrl,
                });
            }

            // If we got results, try to fetch content from the first one
            if (results.Count > 0)
            {
                await EnrichFirstResultAsync(results[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "USAFacts search failed for query: {Query}", query);
        }

        return results;
    }

    private async Task EnrichFirstResultAsync(FactResult result)
    {
        try
        {
            var response = await _http.GetAsync(result.Url);
            if (!response.IsSuccessStatusCode) return;

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to extract article content
            var paragraphs = doc.DocumentNode.SelectNodes("//article//p | //main//p | //div[contains(@class,'content')]//p");
            if (paragraphs is not null)
            {
                var content = string.Join(" ", paragraphs
                    .Take(5)
                    .Select(p => p.InnerText.Trim())
                    .Where(t => t.Length > 20));

                if (content.Length > 0)
                {
                    result.Content = content.Length > 800 ? content[..800] : content;
                }
            }
        }
        catch
        {
            // silently fail enrichment
        }
    }
}
