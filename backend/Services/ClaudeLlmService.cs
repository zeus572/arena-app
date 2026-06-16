using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class ClaudeLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly FactCheckService _factCheck;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClaudeLlmService> _logger;

    public ClaudeLlmService(
        HttpClient http,
        IConfiguration config,
        FactCheckService factCheck,
        IServiceScopeFactory scopeFactory,
        ILogger<ClaudeLlmService> logger)
    {
        _http = http;
        _config = config;
        _factCheck = factCheck;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.anthropic.com/");
        _http.DefaultRequestHeaders.Add("x-api-key", _config["Anthropic:ApiKey"]);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<LlmTurnResult> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns, TurnType turnType = TurnType.Argument, string? crowdQuestion = null, Agent? opponent = null)
    {
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-6";
        var formatConfig = DebateFormatConfig.Get(debate.Format);
        var maxToolRounds = formatConfig.HasTools ? formatConfig.MaxToolRounds : 0;
        var tools = formatConfig.HasTools ? FactCheckService.GetToolDefinitions().ToList() : new List<object>();
        var citations = new List<Citation>();

        // Load agent sources if celebrity/historical
        List<AgentSource>? agentSources = null;
        if (agent.AgentType is "celebrity" or "historical")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();
            agentSources = await db.AgentSources
                .Where(s => s.AgentId == agent.Id)
                .OrderBy(s => s.Priority)
                .ToListAsync();

            if (agentSources.Count > 0)
            {
                tools.Add(new
                {
                    name = "search_agent_sources",
                    description = "Search your personal source library — books, speeches, letters, and documented positions. Use this to find specific quotes and positions that are authentically yours.",
                    input_schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string" }
                        },
                        required = new[] { "query" }
                    }
                });
            }
        }

        var systemPrompt = LlmPromptBuilder.BuildSystemPrompt(agent, debate, turnType, formatConfig, agentSources, opponent);

        var messages = new List<object>();
        foreach (var turn in previousTurns.OrderBy(t => t.TurnNumber))
        {
            var role = turn.AgentId == agent.Id ? "assistant" : "user";
            messages.Add(new Dictionary<string, object> { ["role"] = role, ["content"] = turn.Content });
        }

        if (messages.Count == 0 || GetRole(messages.Last()) == "assistant")
        {
            var prompt = LlmPromptBuilder.BuildUserPrompt(turnType, debate.Format, crowdQuestion, agent, debate.Topic);
            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = prompt
            });
        }

        _logger.LogInformation("Calling Claude API for agent {AgentName} on '{Topic}' (format={Format}, turn {TurnNum})",
            agent.Name, debate.Topic, debate.Format, previousTurns.Count + 1);

        for (var round = 0; round < maxToolRounds + 1; round++)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = formatConfig.MaxTokens,
                ["system"] = systemPrompt,
                ["messages"] = messages,
            };

            if (tools.Count > 0)
                requestBody["tools"] = tools;

            var json = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("v1/messages", httpContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error {Status}: {Body}", response.StatusCode, responseBody);
                throw new InvalidOperationException($"Claude API returned {response.StatusCode}: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var stopReason = doc.RootElement.GetProperty("stop_reason").GetString();
            var contentBlocks = doc.RootElement.GetProperty("content");

            if (stopReason == "end_turn")
            {
                var text = ExtractText(contentBlocks);

                if (debate.Format == "tweet" && text.Length > 280)
                {
                    text = LlmPromptBuilder.EnforceTweetLength(text);
                }

                return new LlmTurnResult { Content = text, Citations = citations };
            }

            if (stopReason == "tool_use")
            {
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = JsonSerializer.Deserialize<JsonElement>(contentBlocks.GetRawText())
                });

                var toolResults = new List<object>();
                foreach (var block in contentBlocks.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() != "tool_use") continue;

                    var toolId = block.GetProperty("id").GetString()!;
                    var toolName = block.GetProperty("name").GetString()!;
                    var input = block.GetProperty("input");
                    var query = input.GetProperty("query").GetString()!;

                    _logger.LogInformation("Tool call: {Tool}('{Query}')", toolName, query);

                    string resultText;

                    if (toolName == "search_agent_sources" && agentSources != null)
                    {
                        var matches = agentSources
                            .Where(s => s.ExcerptText.Contains(query, StringComparison.OrdinalIgnoreCase)
                                     || s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                                     || (s.ThemeTag != null && s.ThemeTag.Contains(query, StringComparison.OrdinalIgnoreCase)))
                            .Take(5)
                            .ToList();

                        resultText = matches.Count > 0
                            ? string.Join("\n\n", matches.Select(s =>
                                $"**{s.Title}** ({s.Author}, {s.Year})\n{s.ExcerptText}"))
                            : "No matching sources found in your personal library.";
                    }
                    else
                    {
                        var results = await ExecuteToolAsync(toolName, query);
                        foreach (var r in results)
                        {
                            if (!string.IsNullOrEmpty(r.Url) && citations.All(c => c.Url != r.Url))
                            {
                                citations.Add(new Citation { Source = r.Source, Title = r.Title, Url = r.Url });
                            }
                        }
                        resultText = results.Count > 0
                            ? string.Join("\n\n", results.Select(r =>
                                $"**{r.Title}** ({r.Source})\n{r.Content}\nSource: {r.Url}"))
                            : "No results found for this query.";
                    }

                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = resultText,
                    });
                }

                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = toolResults
                });

                continue;
            }

            var fallbackText = ExtractText(contentBlocks);
            return new LlmTurnResult { Content = fallbackText, Citations = citations };
        }

        _logger.LogWarning("Tool-use loop hit max rounds, forcing final response without tools");
        var finalRequest = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = formatConfig.MaxTokens,
            ["system"] = systemPrompt,
            ["messages"] = messages,
        };
        var finalJson = JsonSerializer.Serialize(finalRequest);
        var finalContent = new StringContent(finalJson, Encoding.UTF8, "application/json");
        var finalResponse = await _http.PostAsync("v1/messages", finalContent);
        var finalBody = await finalResponse.Content.ReadAsStringAsync();
        if (finalResponse.IsSuccessStatusCode)
        {
            using var finalDoc = JsonDocument.Parse(finalBody);
            var text = ExtractText(finalDoc.RootElement.GetProperty("content"));
            return new LlmTurnResult { Content = text, Citations = citations };
        }
        throw new InvalidOperationException($"Final Claude call failed: {finalResponse.StatusCode}");
    }

    public async Task<CommentaryResult> GenerateCommentaryAsync(Agent commentatorA, Agent commentatorB, Debate debate, List<Turn> previousTurns)
    {
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-6";
        var formatConfig = DebateFormatConfig.Get(debate.Format);

        var systemPrompt = LlmPromptBuilder.BuildCommentarySystemPrompt(commentatorA, commentatorB, debate, formatConfig);
        var userPrompt = LlmPromptBuilder.BuildCommentaryUserPrompt(previousTurns);

        var messages = new List<object>
        {
            new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = userPrompt
            }
        };

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = 400,
            ["system"] = systemPrompt,
            ["messages"] = messages,
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("v1/messages", httpContent);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error for commentary: {Status} {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude API returned {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var text = ExtractText(doc.RootElement.GetProperty("content"));

        return LlmPromptBuilder.ParseCommentary(text, commentatorA.Name, commentatorB.Name);
    }

    private async Task<List<FactProviders.FactResult>> ExecuteToolAsync(string toolName, string query)
    {
        var providerName = toolName switch
        {
            "search_usafacts" => "USAFacts",
            "search_wikipedia" => "Wikipedia",
            "search_web" => "WebSearch",
            "search_budget" => "BudgetData",
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
        };

        return await _factCheck.SearchProviderAsync(providerName, query);
    }

    private static string ExtractText(JsonElement contentBlocks)
    {
        var sb = new StringBuilder();
        foreach (var block in contentBlocks.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                sb.Append(block.GetProperty("text").GetString());
            }
        }
        return sb.Length > 0 ? sb.ToString() : throw new InvalidOperationException("Claude returned no text content.");
    }

    private static string GetRole(object msg)
    {
        if (msg is Dictionary<string, object> dict && dict.TryGetValue("role", out var role))
            return role?.ToString() ?? "";
        var json = JsonSerializer.Serialize(msg);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("role").GetString() ?? "";
    }
}
