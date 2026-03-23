using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Arena.API.Models;

namespace Arena.API.Services;

public class ClaudeLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ClaudeLlmService> _logger;

    public ClaudeLlmService(HttpClient http, IConfiguration config, ILogger<ClaudeLlmService> logger)
    {
        _http = http;
        _config = config;

        _http.BaseAddress = new Uri("https://api.anthropic.com/");
        _http.DefaultRequestHeaders.Add("x-api-key", _config["Anthropic:ApiKey"]);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        _logger = logger;
    }

    public async Task<string> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns)
    {
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514";

        var systemPrompt = $"""
            You are "{agent.Name}", a debate AI with the following persona: {agent.Persona}.
            {(agent.Description is not null ? $"Description: {agent.Description}" : "")}

            You are participating in a structured debate on the topic: "{debate.Topic}"
            {(debate.Description is not null ? $"Context: {debate.Description}" : "")}

            Rules:
            - Present clear, well-reasoned arguments from your persona's perspective
            - Respond directly to your opponent's points when applicable
            - Be persuasive but intellectually honest
            - Keep your response to 2-3 paragraphs
            - Do not break character or reference being an AI
            """;

        var messages = new List<object>();

        foreach (var turn in previousTurns.OrderBy(t => t.TurnNumber))
        {
            var role = turn.AgentId == agent.Id ? "assistant" : "user";
            messages.Add(new { role, content = turn.Content });
        }

        // Add the prompt for the next turn
        if (messages.Count == 0 || messages.Last() is { } last && GetRole(last) == "assistant")
        {
            messages.Add(new { role = "user", content = "Present your argument." });
        }

        var requestBody = new
        {
            model,
            max_tokens = 700,
            system = systemPrompt,
            messages,
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling Claude API for agent {AgentName} on debate '{Topic}' (turn {TurnNum})",
            agent.Name, debate.Topic, previousTurns.Count + 1);

        var response = await _http.PostAsync("v1/messages", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude API returned {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return text ?? throw new InvalidOperationException("Claude returned empty content.");
    }

    private static string GetRole(object msg)
    {
        var json = JsonSerializer.Serialize(msg);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("role").GetString() ?? "";
    }
}
