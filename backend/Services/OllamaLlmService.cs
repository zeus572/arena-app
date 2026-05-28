using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

// Generates debate turns using a local Ollama server (Gemma, Llama, Mistral, ...).
// Intended for dev/eval and eventually opt-in prod use.
//
// Scope of v1:
//   - Hits Ollama's native /api/chat endpoint (same system/messages shape as Claude).
//   - No tool use. Most local models support either no function-calling or a
//     non-uniform dialect. Skipping tools keeps the eval surface clean and means
//     output quality compares prompt+model, not "did the model figure out tool
//     calls." Celebrity/historical agents still get their source library inlined
//     into the system prompt via LlmPromptBuilder, so character grounding works.
//   - No live fact-checks — Citations comes back empty. Reviewers should know
//     factual claims won't be backed by USAFacts/Wikipedia/etc. in this mode.
public class OllamaLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OllamaLlmService> _logger;

    public OllamaLlmService(
        HttpClient http,
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<OllamaLlmService> logger)
    {
        _http = http;
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;

        var baseUrl = _config["Llm:Ollama:BaseUrl"] ?? "http://localhost:11434";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        // Local generation on a CPU/8B-model laptop can easily take 30-90s per
        // turn, especially for longform. Default HttpClient timeout (100s) is
        // too tight.
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<LlmTurnResult> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns, TurnType turnType = TurnType.Argument, string? crowdQuestion = null, Agent? opponent = null)
    {
        var model = _config["Llm:Ollama:Model"] ?? "llama3.1:8b";
        var formatConfig = DebateFormatConfig.Get(debate.Format);

        List<AgentSource>? agentSources = null;
        if (agent.AgentType is "celebrity" or "historical")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();
            agentSources = await db.AgentSources
                .Where(s => s.AgentId == agent.Id)
                .OrderBy(s => s.Priority)
                .ToListAsync();
        }

        var systemPrompt = LlmPromptBuilder.BuildSystemPrompt(agent, debate, turnType, formatConfig, agentSources, opponent);

        var messages = new List<Dictionary<string, string>>
        {
            new() { ["role"] = "system", ["content"] = systemPrompt }
        };

        foreach (var turn in previousTurns.OrderBy(t => t.TurnNumber))
        {
            var role = turn.AgentId == agent.Id ? "assistant" : "user";
            messages.Add(new Dictionary<string, string> { ["role"] = role, ["content"] = turn.Content });
        }

        if (messages.Count == 1 || messages.Last()["role"] == "assistant")
        {
            var prompt = LlmPromptBuilder.BuildUserPrompt(turnType, debate.Format, crowdQuestion, agent, debate.Topic);
            messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = prompt });
        }

        _logger.LogInformation("Calling Ollama ({Model}) for agent {AgentName} on '{Topic}' (format={Format}, turn {TurnNum})",
            model, agent.Name, debate.Topic, debate.Format, previousTurns.Count + 1);

        var text = await ChatAsync(model, messages, formatConfig.MaxTokens);

        if (debate.Format == "tweet" && text.Length > 280)
        {
            text = LlmPromptBuilder.EnforceTweetLength(text);
        }

        return new LlmTurnResult { Content = text, Citations = new List<Citation>() };
    }

    public async Task<CommentaryResult> GenerateCommentaryAsync(Agent commentatorA, Agent commentatorB, Debate debate, List<Turn> previousTurns)
    {
        var model = _config["Llm:Ollama:Model"] ?? "llama3.1:8b";
        var formatConfig = DebateFormatConfig.Get(debate.Format);

        var systemPrompt = LlmPromptBuilder.BuildCommentarySystemPrompt(commentatorA, commentatorB, debate, formatConfig);
        var userPrompt = LlmPromptBuilder.BuildCommentaryUserPrompt(previousTurns);

        var messages = new List<Dictionary<string, string>>
        {
            new() { ["role"] = "system", ["content"] = systemPrompt },
            new() { ["role"] = "user",   ["content"] = userPrompt   },
        };

        var text = await ChatAsync(model, messages, maxTokens: 400);

        return LlmPromptBuilder.ParseCommentary(text, commentatorA.Name, commentatorB.Name);
    }

    private async Task<string> ChatAsync(string model, List<Dictionary<string, string>> messages, int maxTokens)
    {
        var body = new
        {
            model,
            messages,
            stream = false,
            options = new
            {
                num_predict = maxTokens,
                temperature = 0.8,
            },
        };

        var json = JsonSerializer.Serialize(body);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("api/chat", httpContent);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ollama API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Ollama API returned {response.StatusCode}: {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var text = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Ollama returned empty content.");
        }
        return text;
    }
}
