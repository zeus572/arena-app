using System.Text;
using System.Text.Json;
using Arena.API.Models;

namespace Arena.API.Services;

public class ClaudeLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly FactCheckService _factCheck;
    private readonly ILogger<ClaudeLlmService> _logger;
    private const int MaxToolRounds = 5;

    public ClaudeLlmService(
        HttpClient http,
        IConfiguration config,
        FactCheckService factCheck,
        ILogger<ClaudeLlmService> logger)
    {
        _http = http;
        _config = config;
        _factCheck = factCheck;
        _logger = logger;

        _http.BaseAddress = new Uri("https://api.anthropic.com/");
        _http.DefaultRequestHeaders.Add("x-api-key", _config["Anthropic:ApiKey"]);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<LlmTurnResult> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns, TurnType turnType = TurnType.Argument, string? crowdQuestion = null)
    {
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514";
        var tools = FactCheckService.GetToolDefinitions();
        var citations = new List<Citation>();

        var systemPrompt = $"""
            You are "{agent.Name}", a debate AI with the following persona: {agent.Persona}.
            {(agent.Description is not null ? $"Description: {agent.Description}" : "")}

            You are participating in a structured debate on the topic: "{debate.Topic}"
            {(debate.Description is not null ? $"Context: {debate.Description}" : "")}

            DEBATE STYLE:
            - You are in a live debate. Be sharp, pointed, and persuasive — not academic.
            - Lead with your strongest punch. Open with a bold claim or a direct counter to your opponent.
            - Respond directly to your opponent's points when applicable — call out weak logic.
            - Use short, punchy paragraphs. Break up your argument into 3-5 short paragraphs for readability.
            - Use **bold** for your key claims and knockout lines that drive your point home.
            - Use *italics* for emphasis on critical words or when quoting sources.
            - Use bullet points or numbered lists when presenting multiple pieces of evidence.
            - Do not break character or reference being an AI.
            - Your response should use markdown formatting (bold, italic, lists, line breaks).

            EVIDENCE AND CITATIONS (CRITICAL):
            - You MUST use the available tools to look up real facts, statistics, and evidence BEFORE writing your argument.
            - Prioritize search_usafacts for US statistics and government data.
            - You may call multiple tools before writing your argument.
            - Use numbered reference markers like [1], [2], [3] inline next to every factual claim.
            - Quote or paraphrase specific data directly, e.g.:
              "According to USAFacts, **federal spending on healthcare reached $1.5 trillion** in 2023 [1]"
              "The Geneva Convention explicitly states: *'...'* [2]"
            - Number your references in the order they first appear.

            BUDGET AND FISCAL POLICY (IMPORTANT):
            - You MUST use the search_budget tool to look up real federal spending data from USASpending.gov.
            - When discussing any policy, quantify its fiscal impact with specific dollar amounts.
            - Propose concrete budget allocations, e.g.: "I propose allocating $X billion to [program]"
            - Compare current spending vs. your proposed spending to make your case tangible.
            - Reference actual agency budgets and program costs.
            """ + (turnType == TurnType.Compromise ? """

            COMPROMISE MODE (ACTIVE):
            - The arbiter has called for compromise. You MUST now seek common ground with your opponent.
            - Acknowledge specific points from your opponent that have merit.
            - Propose concrete concessions you are willing to make.
            - Use search_budget to propose a specific compromise budget with real dollar amounts.
            - Be genuine — find actual middle ground, don't just restate your original position.

            BUDGET TABLE (REQUIRED):
            - You MUST include a markdown table summarizing your compromise budget proposal.
            - The table should have columns: Category/Program | Current Spending | Proposed Change | New Amount
            - Include at least 4-6 line items showing specific programs or agencies.
            - Add a **Total** row at the bottom.
            - Use dollar amounts with B/M suffixes (e.g., $886.4B, $72.3M).
            - After the table, add a brief summary of net fiscal impact.
            - Example format:
              | Program | Current | Change | Proposed |
              |---------|---------|--------|----------|
              | Defense | $886B | -$50B | $836B |
              | Education | $80B | +$20B | $100B |
              | **Total** | **$966B** | **-$30B** | **$936B** |
            """ : "");

        // Build message history
        var messages = new List<object>();
        foreach (var turn in previousTurns.OrderBy(t => t.TurnNumber))
        {
            var role = turn.AgentId == agent.Id ? "assistant" : "user";
            messages.Add(new Dictionary<string, object> { ["role"] = role, ["content"] = turn.Content });
        }

        if (messages.Count == 0 || GetRole(messages.Last()) == "assistant")
        {
            var prompt = turnType == TurnType.Compromise
                ? "Propose your compromise. Acknowledge your opponent's valid points and suggest concrete budget concessions. Use search_budget to ground your proposal in real numbers."
                : "Present your argument. Use the fact-checking tools to find real evidence and data to support your position.";

            if (!string.IsNullOrEmpty(crowdQuestion))
            {
                prompt += $"\n\nIMPORTANT — A member of the audience has asked a question you must address in your response: \"{crowdQuestion}\"";
            }

            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = prompt
            });
        }

        _logger.LogInformation("Calling Claude API with tools for agent {AgentName} on '{Topic}' (turn {TurnNum})",
            agent.Name, debate.Topic, previousTurns.Count + 1);

        // Tool-use loop
        for (var round = 0; round < MaxToolRounds + 1; round++)
        {
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["max_tokens"] = 1024,
                ["system"] = systemPrompt,
                ["messages"] = messages,
                ["tools"] = tools,
            };

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

            // If stop_reason is "end_turn", extract text and we're done
            if (stopReason == "end_turn")
            {
                var text = ExtractText(contentBlocks);
                return new LlmTurnResult { Content = text, Citations = citations };
            }

            // If stop_reason is "tool_use", process tool calls
            if (stopReason == "tool_use")
            {
                // Add assistant message with the full content array
                var assistantContent = JsonSerializer.Deserialize<List<object>>(contentBlocks.GetRawText())!;
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = JsonSerializer.Deserialize<JsonElement>(contentBlocks.GetRawText())
                });

                // Process each tool_use block
                var toolResults = new List<object>();
                foreach (var block in contentBlocks.EnumerateArray())
                {
                    if (block.GetProperty("type").GetString() != "tool_use") continue;

                    var toolId = block.GetProperty("id").GetString()!;
                    var toolName = block.GetProperty("name").GetString()!;
                    var input = block.GetProperty("input");
                    var query = input.GetProperty("query").GetString()!;

                    _logger.LogInformation("Tool call: {Tool}('{Query}')", toolName, query);

                    var results = await ExecuteToolAsync(toolName, query);

                    // Collect citations
                    foreach (var r in results)
                    {
                        if (!string.IsNullOrEmpty(r.Url) && citations.All(c => c.Url != r.Url))
                        {
                            citations.Add(new Citation
                            {
                                Source = r.Source,
                                Title = r.Title,
                                Url = r.Url,
                            });
                        }
                    }

                    // Format results as tool_result content
                    var resultText = results.Count > 0
                        ? string.Join("\n\n", results.Select(r =>
                            $"**{r.Title}** ({r.Source})\n{r.Content}\nSource: {r.Url}"))
                        : "No results found for this query.";

                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = toolId,
                        content = resultText,
                    });
                }

                // Add tool results as user message
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = toolResults
                });

                continue; // Next round
            }

            // Unexpected stop reason — extract whatever text we have
            var fallbackText = ExtractText(contentBlocks);
            return new LlmTurnResult { Content = fallbackText, Citations = citations };
        }

        // Max rounds exceeded — make one final call without tools to force a text response
        _logger.LogWarning("Tool-use loop hit max rounds, forcing final response without tools");
        var finalRequest = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = 1024,
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
