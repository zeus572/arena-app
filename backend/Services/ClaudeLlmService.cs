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
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514";
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

            // Add search_agent_sources tool
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

        var systemPrompt = BuildSystemPrompt(agent, debate, turnType, formatConfig, agentSources, opponent);

        // Build message history
        var messages = new List<object>();
        foreach (var turn in previousTurns.OrderBy(t => t.TurnNumber))
        {
            var role = turn.AgentId == agent.Id ? "assistant" : "user";
            messages.Add(new Dictionary<string, object> { ["role"] = role, ["content"] = turn.Content });
        }

        if (messages.Count == 0 || GetRole(messages.Last()) == "assistant")
        {
            var prompt = BuildUserPrompt(turnType, debate.Format, crowdQuestion, agent);
            messages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = prompt
            });
        }

        _logger.LogInformation("Calling Claude API for agent {AgentName} on '{Topic}' (format={Format}, turn {TurnNum})",
            agent.Name, debate.Topic, debate.Format, previousTurns.Count + 1);

        // Tool-use loop
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

                // Post-generation enforcement for tweet format
                if (debate.Format == "tweet" && text.Length > 280)
                {
                    text = EnforceTweetLength(text);
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

        // Max rounds exceeded — force final response without tools
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

    private static string BuildSystemPrompt(Agent agent, Debate debate, TurnType turnType, DebateFormatConfig formatConfig, List<AgentSource>? agentSources, Agent? opponent)
    {
        var sb = new StringBuilder();

        // Base persona
        sb.AppendLine($"""
            You are "{agent.Name}", a debate AI with the following persona: {agent.Persona}.
            {(agent.Description is not null ? $"Description: {agent.Description}" : "")}

            You are participating in a structured debate on the topic: "{debate.Topic}"
            {(debate.Description is not null ? $"Context: {debate.Description}" : "")}
            """);

        // Source library for celebrity/historical agents
        if (agentSources is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("SOURCE LIBRARY — You must stay in character using these primary sources:");
            sb.AppendLine();
            for (var i = 0; i < agentSources.Count; i++)
            {
                var s = agentSources[i];
                var yearStr = s.Year.HasValue ? $" ({s.Year})" : "";
                sb.AppendLine($"[SL-{i + 1}] \"{s.Title}\"{yearStr} — {s.ExcerptText}");
            }
            sb.AppendLine();
            sb.AppendLine("""
                RULES FOR SOURCE USAGE:
                - You MUST cite at least one source from your library per response using [SL-1], [SL-2] etc.
                - Stay consistent with the positions documented in these sources.
                - When facts from your source library conflict with tool results, acknowledge both but maintain your character's known position.
                - You may reference sources not in your library, but your PRIMARY voice comes from these.
                """);
        }

        // Temporal context for historical agents
        if (agent.AgentType == "historical" && agent.Era != null)
        {
            sb.AppendLine($"""

                TEMPORAL CONTEXT:
                - You are {agent.Name} from the {agent.Era} era. You bring the values, knowledge, and rhetorical style of your time.
                - When asked about modern issues, reason from your documented principles. If you wrote about federal vs. state power, apply that logic to modern federalism questions.
                - You may express genuine confusion or curiosity about modern technology or institutions that did not exist in your time — this is entertaining and authentic.
                - Do NOT pretend to have modern knowledge you would not have. Reason from first principles.
                """);
        }

        // Legal disclaimer for celebrity/historical agents
        if (agent.AgentType is "celebrity" or "historical")
        {
            sb.AppendLine($"""

                DISCLAIMER BEHAVIOR:
                - You are a simulation inspired by {agent.Name}'s documented public positions and writings.
                - You are NOT the real {agent.Name} and do not claim to be.
                - Stay grounded in documented sources. Do not fabricate positions the real person never held.
                - If asked about something with no documented stance, say: "I haven't spoken on this specifically, but based on my principles..."
                """);
        }

        // Format-specific prompts
        switch (debate.Format)
        {
            case "common_ground":
                var opponentName = opponent?.Name ?? "your opponent";
                sb.AppendLine($"""

                    COMMON GROUND MODE (ACTIVE):
                    - You are {agent.Name} and you are here to find GENUINE agreement with {opponentName}.
                    - This is NOT about being nice or vague. Find SPECIFIC policy positions, values, or principles where you actually agree — and cite evidence.
                    - Stay completely in character. If you are Donald Trump finding common ground with Bernie Sanders, you sound like Trump acknowledging specific Sanders points, not a diplomat writing a communique.
                    - You MUST identify at least 2 concrete, specific areas of agreement per turn.
                    - Each agreement must include:
                      (a) The specific policy or principle
                      (b) Why YOU support it (from your perspective/sources)
                      (c) Why your opponent supports it (acknowledging their reasoning)
                      (d) A factual citation supporting the shared position
                    - Do NOT agree on platitudes like "we both want what's best for America." That is lazy. Find real policy overlap.
                    """);
                break;

            case "tweet":
                sb.AppendLine("""

                    HOT TAKE MODE (ACTIVE):
                    - Your response MUST be 280 characters or less. Non-negotiable.
                    - Write like a short-form social post. Hashtags allowed. Handles encouraged.
                    - Be punchy. Be memorable. No hedging.
                    - One fact-checking tool per turn max. Keep citations ultra-brief.
                    - Think: "the hot take that goes viral because it's devastatingly correct."
                    - No bullet points. No markdown headers. One raw, devastating take.
                    """);
                break;

            case "rapid_fire":
                sb.AppendLine("""

                    RAPID FIRE MODE (ACTIVE):
                    - Respond in 1-2 sentences MAXIMUM. Not three. Not a paragraph.
                    - Counter your opponent's last point directly.
                    - Speed over depth. Hit hard, move on.
                    - No tool use — argue from known positions and general knowledge.
                    - Think: the 10-second clip from a presidential debate that goes viral.
                    """);
                break;

            case "longform":
                sb.AppendLine("""

                    LONGFORM ESSAY MODE (ACTIVE):
                    - Write a substantive, well-structured essay of 500-800 words.
                    - Use section headers (##) to organize your argument.
                    - Cite at least 4 sources using fact-checking tools.
                    - This is your definitive statement on this topic. Make it count.
                    - Academic tone acceptable, but don't lose your character voice entirely.
                    - Include a "Summary of Position" section at the end (2-3 sentences).
                    """);
                break;

            case "roast":
                sb.AppendLine("""

                    ROAST BATTLE MODE (ACTIVE):
                    - You are in a political roast battle. Destroy your opponent's position with HUMOR.
                    - Lead with jokes. Sarcasm, wordplay, analogies, callbacks all count.
                    - You may exaggerate for comedic effect, but your underlying point should be valid.
                    - Reference your opponent's known positions and track record for maximum burn.
                    - Keep it about POLICY and POSITIONS — not personal attacks on appearance or family.
                    - Think: the funniest person at the White House Correspondents' Dinner who also happens to be right about policy.
                    - Open with your best roast line. Follow with 1-2 supporting jokes. Close with a callback.
                    """);
                break;

            case "town_hall" when turnType == TurnType.Question:
                var respondentName = opponent?.Name ?? "the respondent";
                sb.AppendLine($"""

                    TOWN HALL QUESTIONER MODE:
                    - Ask ONE pointed question to {respondentName}.
                    - Make it specific and hard to dodge.
                    - Briefly explain why you're asking (1-2 sentences) then pose the question.
                    - Stay in character — your question reflects YOUR values and concerns.
                    - Do not argue. Just ask. Put {respondentName} on the spot.
                    """);
                break;

            case "town_hall":
                sb.AppendLine("""

                    TOWN HALL RESPONDENT MODE:
                    - You MUST directly answer the question just asked. No dodging, no pivoting.
                    - After answering, you may briefly reinforce your broader position.
                    - Use fact-checking tools to support your answer with real data.
                    - Be direct. The audience is watching. A non-answer will be noticed.
                    """);
                break;

            default: // standard format
                sb.AppendLine("""

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
                    - Quote or paraphrase specific data directly.
                    - Number your references in the order they first appear.

                    BUDGET AND FISCAL POLICY (IMPORTANT):
                    - You MUST use the search_budget tool to look up real federal spending data from USASpending.gov.
                    - When discussing any policy, quantify its fiscal impact with specific dollar amounts.
                    - Propose concrete budget allocations.
                    - Compare current spending vs. your proposed spending to make your case tangible.
                    - Reference actual agency budgets and program costs.
                    """);
                break;
        }

        // Turn type overrides (for standard format mainly)
        if (turnType == TurnType.Wildcard)
        {
            sb.AppendLine("""

                WILDCARD MODE (ACTIVE):
                - You have been INJECTED into this debate as a surprise wildcard guest.
                - Open with a dramatic entrance line acknowledging you've crashed the debate.
                - Stay completely in your persona character.
                - Address both debaters directly by referencing their previous arguments.
                - Keep your response concise (2-3 short paragraphs).
                - Do NOT pick a side — your role is to challenge, entertain, or provoke deeper thought.
                """);
        }
        else if (turnType == TurnType.Compromise)
        {
            sb.AppendLine("""

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
                """);
        }

        // Character anchor reminder for celebrity agents
        if (agent.AgentType is "celebrity" or "historical")
        {
            sb.AppendLine($"\nREMINDER: Stay in character as {agent.Name}. Maintain your authentic voice and perspective throughout.");
        }

        return sb.ToString();
    }

    private static string BuildUserPrompt(TurnType turnType, string format, string? crowdQuestion, Agent agent)
    {
        var prompt = (turnType, format) switch
        {
            (TurnType.Compromise, _) => "Propose your compromise. Acknowledge your opponent's valid points and suggest concrete budget concessions. Use search_budget to ground your proposal in real numbers.",
            (TurnType.Agreement, _) => "Find genuine areas of agreement with your opponent. Identify specific policies or principles you share and cite evidence for each.",
            (TurnType.Question, _) => "Ask your pointed question to the respondent. Make it specific and hard to dodge.",
            (TurnType.Roast, _) => "Deliver your roast. Lead with humor, back it up with policy, close with a callback.",
            (_, "tweet") => "Post your hot take. 280 characters max. Make it count.",
            (_, "rapid_fire") => "Fire back. 1-2 sentences max. Counter their last point directly.",
            (_, "longform") => "Write your essay. 500-800 words. Cite at least 4 sources.",
            (_, "roast") => "Deliver your roast. Lead with humor, back it up with policy.",
            (_, "common_ground") => "Find genuine common ground. Identify at least 2 specific areas of agreement with citations.",
            _ => "Present your argument. Use the fact-checking tools to find real evidence and data to support your position.",
        };

        if (!string.IsNullOrEmpty(crowdQuestion))
        {
            prompt += $"\n\nIMPORTANT — A member of the audience has asked a question you must address in your response: \"{crowdQuestion}\"";
        }

        return prompt;
    }

    private static string EnforceTweetLength(string text)
    {
        if (text.Length <= 280) return text;

        // Find last complete sentence under 280
        var truncated = text[..280];
        var lastPeriod = truncated.LastIndexOfAny(new[] { '.', '!', '?' });
        if (lastPeriod > 100)
            return truncated[..(lastPeriod + 1)];

        return truncated;
    }

    public async Task<CommentaryResult> GenerateCommentaryAsync(Agent commentatorA, Agent commentatorB, Debate debate, List<Turn> previousTurns)
    {
        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514";
        var formatConfig = DebateFormatConfig.Get(debate.Format);

        // Format-aware commentary additions
        var formatCommentary = debate.Format switch
        {
            "tweet" => "\n- Comment on the best hot takes and who's winning the thread.",
            "roast" => "\n- Score the roasts. Who got the biggest laugh? Who flopped?",
            "common_ground" => "\n- React to surprising agreements. Is this genuine or performative?",
            "town_hall" => "\n- Grade the respondent's answers. Are they dodging? Who asked the toughest question?",
            _ => ""
        };

        var systemPrompt = $"""
            You are writing dialogue for two debate commentators in a live commentary booth.

            Commentator 1: {commentatorA.Name} — {commentatorA.Persona}
            Commentator 2: {commentatorB.Name} — {commentatorB.Persona}

            The debate topic is: "{debate.Topic}"
            {(debate.Description is not null ? $"Context: {debate.Description}" : "")}
            Debate format: {formatConfig.DisplayName}

            COMMENTARY RULES:
            - This is a sports-style commentary booth — keep it entertaining, snappy, and fun.
            - Each commentator speaks 1-3 sentences MAX. Brevity is key.
            - Focus on: who's winning, strong/weak moments, rhetorical highlights, entertainment.
            - They should play off each other — agree, disagree, build on each other's takes.
            - Do NOT summarize arguments in full — just react to key moments.
            - Reference specific things the debaters said or did.
            - Be opinionated! Take positions on who's doing better.
            - Use casual, conversational language — not academic.
            - Do not break character or reference being AI.{formatCommentary}

            FORMAT (you MUST follow this exactly):
            [{commentatorA.Name}]: <their commentary>
            [{commentatorB.Name}]: <their commentary>
            """;

        var recentTurns = previousTurns
            .OrderBy(t => t.TurnNumber)
            .TakeLast(6)
            .ToList();

        var turnSummary = string.Join("\n", recentTurns.Select(t =>
        {
            var typeLabel = t.Type != TurnType.Argument ? $" [{t.Type}]" : "";
            var snippet = t.Content.Length > 300 ? t.Content[..300] + "..." : t.Content;
            return $"Turn {t.TurnNumber}{typeLabel} ({t.Agent?.Name ?? "Agent"}): {snippet}";
        }));

        var messages = new List<object>
        {
            new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = $"Here are the recent turns in the debate. Provide your commentary booth reaction:\n\n{turnSummary}"
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

        return ParseCommentary(text, commentatorA.Name, commentatorB.Name);
    }

    private static CommentaryResult ParseCommentary(string text, string nameA, string nameB)
    {
        var result = new CommentaryResult();

        var markerA = $"[{nameA}]:";
        var markerB = $"[{nameB}]:";

        var idxA = text.IndexOf(markerA, StringComparison.OrdinalIgnoreCase);
        var idxB = text.IndexOf(markerB, StringComparison.OrdinalIgnoreCase);

        if (idxA >= 0 && idxB >= 0)
        {
            if (idxA < idxB)
            {
                result.CommentatorAContent = text[(idxA + markerA.Length)..idxB].Trim();
                result.CommentatorBContent = text[(idxB + markerB.Length)..].Trim();
            }
            else
            {
                result.CommentatorBContent = text[(idxB + markerB.Length)..idxA].Trim();
                result.CommentatorAContent = text[(idxA + markerA.Length)..].Trim();
            }
        }
        else
        {
            result.CommentatorAContent = text.Trim();
            result.CommentatorBContent = "Couldn't have said it better myself!";
        }

        return result;
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
