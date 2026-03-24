using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class NewsTopicService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NewsTopicService> _logger;
    private readonly HttpClient _http;

    // Neutral, no-API-key RSS feeds
    private static readonly string[] RssFeeds =
    {
        "https://feeds.npr.org/1001/rss.xml",          // NPR News
        "https://rss.app/feeds/v1.1/tSmBDoO3eVHDGaQl.xml", // AP News (via rss.app proxy)
        "https://feeds.bbci.co.uk/news/world/rss.xml",  // BBC World
        "https://feeds.bbci.co.uk/news/rss.xml",        // BBC Top Stories
    };

    public NewsTopicService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<NewsTopicService> logger,
        HttpClient http)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ArenaBot/1.0)");
    }

    public async Task GenerateTopicsFromNewsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("NewsTopicService: Fetching headlines from RSS feeds...");

        var headlines = await FetchHeadlinesAsync(ct);
        if (headlines.Count == 0)
        {
            _logger.LogWarning("NewsTopicService: No headlines fetched, skipping topic generation");
            return;
        }

        _logger.LogInformation("NewsTopicService: Fetched {Count} headlines, generating debate topics...", headlines.Count);

        var debateTopics = await GenerateDebateQuestionsAsync(headlines, ct);
        if (debateTopics.Count == 0)
        {
            _logger.LogWarning("NewsTopicService: LLM returned no topics");
            return;
        }

        // Moderate generated topics
        using var scope = _scopeFactory.CreateScope();
        var moderation = scope.ServiceProvider.GetRequiredService<TopicModerationService>();
        var approved = await moderation.FilterTopicsAsync(debateTopics);
        _logger.LogInformation("NewsTopicService: {Approved}/{Total} topics passed moderation",
            approved.Count, debateTopics.Count);
        debateTopics = approved;

        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        var existingTopics = await db.Set<GeneratedTopic>()
            .Select(t => t.Title.ToLower())
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingTopics, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var topic in debateTopics)
        {
            if (existingSet.Contains(topic)) continue;

            db.Set<GeneratedTopic>().Add(new GeneratedTopic
            {
                Id = Guid.NewGuid(),
                Title = topic,
                Source = "news",
            });
            existingSet.Add(topic);
            added++;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("NewsTopicService: Added {Added} new topics from news (total generated: {Total})",
            added, debateTopics.Count);
    }

    private async Task<List<string>> FetchHeadlinesAsync(CancellationToken ct)
    {
        var headlines = new List<string>();

        foreach (var feedUrl in RssFeeds)
        {
            try
            {
                var response = await _http.GetStringAsync(feedUrl, ct);
                using var reader = XmlReader.Create(new StringReader(response));
                var feed = SyndicationFeed.Load(reader);

                foreach (var item in feed.Items.Take(15))
                {
                    var title = item.Title?.Text?.Trim();
                    if (!string.IsNullOrEmpty(title) && title.Length > 15)
                        headlines.Add(title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch RSS feed: {Url}", feedUrl);
            }
        }

        // Deduplicate and shuffle
        return headlines
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => Random.Shared.Next())
            .Take(30)
            .ToList();
    }

    private async Task<List<string>> GenerateDebateQuestionsAsync(List<string> headlines, CancellationToken ct)
    {
        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("NewsTopicService: No Anthropic API key configured, skipping LLM generation");
            return [];
        }

        var headlineBlock = string.Join("\n", headlines.Select((h, i) => $"{i + 1}. {h}"));

        var requestBody = new
        {
            model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514",
            max_tokens = 2048,
            system = """
                You are a debate topic generator for a political debate platform.
                Given a list of recent news headlines, generate thought-provoking debate questions.
                Each question should be debatable from multiple political perspectives.
                Focus on policy implications, not just the news event itself.
                Make questions concise (under 80 characters) and start with "Should", "Is", "Can", "Does", or "Will".
                Return ONLY a JSON array of strings, no other text.
                Generate 8-12 unique questions.
                """,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $"Generate debate questions from these recent headlines:\n\n{headlineBlock}"
                }
            }
        };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error for topic generation: {Status} {Body}", response.StatusCode, body);
                return [];
            }

            using var doc = JsonDocument.Parse(body);
            var textBlock = doc.RootElement.GetProperty("content").EnumerateArray()
                .FirstOrDefault(b => b.GetProperty("type").GetString() == "text");

            var text = textBlock.GetProperty("text").GetString() ?? "";

            // Extract JSON array from the response (may have markdown fences)
            var jsonStart = text.IndexOf('[');
            var jsonEnd = text.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd < 0) return [];

            var jsonArray = text[jsonStart..(jsonEnd + 1)];
            var topics = JsonSerializer.Deserialize<List<string>>(jsonArray) ?? [];

            return topics
                .Where(t => t.Length > 15 && t.Length < 120 && t.EndsWith('?'))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate debate topics from news");
            return [];
        }
    }
}
