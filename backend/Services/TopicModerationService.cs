using System.Text;
using System.Text.Json;

namespace Arena.API.Services;

public class TopicModerationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<TopicModerationService> _logger;

    public TopicModerationService(IConfiguration config, ILogger<TopicModerationService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Returns null if the topic is appropriate, or a rejection reason if not.
    /// </summary>
    public async Task<string?> CheckTopicAsync(string topic)
    {
        // Fast keyword pre-filter
        var lower = topic.ToLowerInvariant();
        var blockedKeywords = new[]
        {
            "sex", "porn", "nude", "erotic", "fetish", "nsfw",
            "scripture", "bible verse", "quran verse", "torah verse", "sermon",
        };

        foreach (var kw in blockedKeywords)
        {
            if (lower.Contains(kw))
                return $"Topic contains inappropriate content.";
        }

        // LLM moderation check
        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("TopicModeration: No API key, skipping LLM check");
            return null; // permissive fallback
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var requestBody = new
            {
                model = "claude-haiku-4-5-20251001",
                max_tokens = 256,
                system = """
                    You are a content moderator for a political debate platform called Debate Arena.
                    The platform hosts AI debates on politics, public policy, economics, governance, social issues, technology policy, and related topics.

                    Evaluate whether a proposed debate topic is appropriate. A topic is APPROPRIATE if it:
                    - Relates to politics, public policy, economics, governance, social issues, law, technology policy, education, healthcare, environment, defense, immigration, or civil rights
                    - Can be debated from multiple political perspectives
                    - Is a genuine policy question, not a troll or joke

                    A topic is INAPPROPRIATE if it:
                    - Is sexual, explicit, or pornographic in nature
                    - Is specifically about religious doctrine, scripture, or theology (general policy questions that touch on religion ARE allowed, e.g. "Should prayer be allowed in public schools?" is fine)
                    - Is completely off-topic (about entertainment, sports scores, recipes, cars, dating, etc.)
                    - Is a personal attack on a specific private individual
                    - Promotes violence or illegal activity

                    Respond with ONLY a JSON object: {"appropriate": true} or {"appropriate": false, "reason": "brief explanation"}
                    """,
                messages = new[]
                {
                    new { role = "user", content = $"Is this debate topic appropriate?\n\n\"{topic}\"" }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await http.PostAsync("https://api.anthropic.com/v1/messages", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TopicModeration API error: {Status}", response.StatusCode);
                return null; // permissive on API failure
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("content").EnumerateArray()
                .FirstOrDefault(b => b.GetProperty("type").GetString() == "text")
                .GetProperty("text").GetString() ?? "";

            // Extract JSON from response
            var jsonStart = text.IndexOf('{');
            var jsonEnd = text.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0) return null;

            var resultJson = text[jsonStart..(jsonEnd + 1)];
            using var result = JsonDocument.Parse(resultJson);

            var appropriate = result.RootElement.GetProperty("appropriate").GetBoolean();
            if (appropriate) return null;

            var reason = result.RootElement.TryGetProperty("reason", out var r)
                ? r.GetString() ?? "Topic is not appropriate for this platform."
                : "Topic is not appropriate for this platform.";

            _logger.LogInformation("TopicModeration rejected: \"{Topic}\" — {Reason}", topic, reason);
            return reason;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TopicModeration check failed, allowing topic");
            return null; // permissive on error
        }
    }

    /// <summary>
    /// Filter a list of topics, returning only appropriate ones.
    /// </summary>
    public async Task<List<string>> FilterTopicsAsync(List<string> topics)
    {
        var results = new List<string>();
        foreach (var topic in topics)
        {
            var rejection = await CheckTopicAsync(topic);
            if (rejection is null)
                results.Add(topic);
        }
        return results;
    }
}
