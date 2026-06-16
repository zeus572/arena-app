using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

/// <summary>
/// Once a day, gathers federal budget data from the fact providers
/// (USASpending.gov + USAFacts) and asks Claude to synthesize "Did You Know?"
/// contradictions — pairs of facts that are both true but create tension when
/// placed side by side. Daily variety comes from a rotating focus state and an
/// alternating 5/10-year historical comparison window.
/// </summary>
public class DailyBudgetFactService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DailyBudgetFactService> _logger;
    private readonly bool _enabled;
    private readonly int _intervalHours;
    private readonly int _factsPerDay;
    private readonly int _retentionDays;

    // States chosen for the federal "taker vs maker" tension — high federal
    // inflows relative to taxes paid, or the reverse.
    private static readonly (string Name, string Fips)[] FocusStates =
    [
        ("Texas", "48"), ("California", "06"), ("West Virginia", "54"), ("New York", "36"),
        ("Mississippi", "28"), ("Wyoming", "56"), ("Florida", "12"), ("Vermont", "50"),
        ("Kentucky", "21"), ("Massachusetts", "25"), ("Alabama", "01"), ("Oregon", "41"),
        ("North Dakota", "38"), ("Illinois", "17"), ("Montana", "30"), ("Georgia", "13"),
        ("Alaska", "02"), ("Ohio", "39"), ("Louisiana", "22"), ("Colorado", "08"),
    ];

    public DailyBudgetFactService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<DailyBudgetFactService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
        _enabled = config.GetValue("BudgetFacts:Enabled", true);
        _intervalHours = config.GetValue("BudgetFacts:IntervalHours", 24);
        _factsPerDay = config.GetValue("BudgetFacts:FactsPerDay", 4);
        _retentionDays = config.GetValue("BudgetFacts:RetentionDays", 7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("DailyBudgetFactService disabled via config");
            return;
        }

        _logger.LogInformation("DailyBudgetFactService started. Interval={Hours}h, FactsPerDay={Count}",
            _intervalHours, _factsPerDay);

        using (var readyScope = _scopeFactory.CreateScope())
            await readyScope.ServiceProvider.GetRequiredService<StartupReadiness>()
                .WaitUntilReadyAsync(stoppingToken);

        // Stagger from the other background services' initial delays
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateFactsIfNeededAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "DailyBudgetFact tick failed");
            }

            await Task.Delay(TimeSpan.FromHours(_intervalHours), stoppingToken);
        }
    }

    /// <summary>Public entry point for the /dev/generate-budget-facts trigger.</summary>
    public Task TriggerNowAsync(CancellationToken ct) => GenerateFactsIfNeededAsync(ct);

    private async Task GenerateFactsIfNeededAsync(CancellationToken ct)
    {
        var apiKey = _config["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("BudgetFacts: no Anthropic API key configured, skipping");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await db.BudgetFacts.CountAsync(f => f.FactDate == today && f.IsActive, ct);
        if (existing >= _factsPerDay)
        {
            _logger.LogInformation("BudgetFacts already generated for {Date} ({Count}), skipping", today, existing);
            return;
        }

        // Prune beyond the retention window
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        await db.BudgetFacts.Where(f => f.GeneratedAt < cutoff).ExecuteDeleteAsync(ct);

        var factCheck = scope.ServiceProvider.GetRequiredService<FactCheckService>();
        var (stateName, currentFy, compareFy) = TodaysFocus();
        var rawData = await GatherRawDataAsync(factCheck, stateName, currentFy, compareFy);
        if (rawData.Length == 0)
        {
            _logger.LogWarning("BudgetFacts: no provider data retrieved, skipping this run");
            return;
        }

        var recentLabels = await db.BudgetFacts
            .Where(f => f.GeneratedAt >= DateTime.UtcNow.AddDays(-7))
            .Select(f => f.Category + ": " + f.TensionLabel)
            .ToListAsync(ct);

        var facts = await SynthesizeContradictionsAsync(
            apiKey, rawData, recentLabels, stateName, currentFy, compareFy, ct);

        if (facts.Count == 0)
        {
            _logger.LogWarning("BudgetFacts: Claude returned no usable contradictions");
            return;
        }

        foreach (var fact in facts)
        {
            fact.FactDate = today;
            db.BudgetFacts.Add(fact);
        }
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Generated {Count} budget facts for {Date} (focus: {State}, FY{Compare}–FY{Current})",
            facts.Count, today, stateName, compareFy, currentFy);
    }

    private static (string StateName, int CurrentFy, int CompareFy) TodaysFocus()
    {
        var now = DateTime.UtcNow;
        var state = FocusStates[now.DayOfYear % FocusStates.Length].Name;
        var currentFy = now.Month >= 10 ? now.Year : now.Year - 1;
        var compareFy = now.DayOfYear % 2 == 0 ? currentFy - 10 : currentFy - 5;
        return (state, currentFy, compareFy);
    }

    private async Task<string> GatherRawDataAsync(
        FactCheckService factCheck, string stateName, int currentFy, int compareFy)
    {
        // Parameterized so each day's data sweep differs (state rotation +
        // alternating historical window); see TodaysFocus().
        (string Provider, string Query)[] queries =
        [
            ("BudgetData", $"federal spending in {stateName}"),
            ("BudgetData", $"defense budget FY{currentFy} vs FY{compareFy}"),
            ("BudgetData", $"social security spending FY{currentFy} vs FY{compareFy}"),
            ("BudgetData", $"federal income tax revenue FY{currentFy}"),
            ("BudgetData", $"national debt interest payments FY{currentFy} vs FY{compareFy}"),
            ("USAFacts", $"{stateName} federal spending per capita"),
            ("USAFacts", $"{stateName} federal taxes paid vs received"),
            ("USAFacts", $"government spending growth {compareFy} to {currentFy}"),
            ("USAFacts", $"tax burden by income level {currentFy}"),
            ("USAFacts", "discretionary vs mandatory spending growth"),
        ];

        var sb = new StringBuilder();
        foreach (var (provider, query) in queries)
        {
            try
            {
                var results = await factCheck.SearchProviderAsync(provider, query, maxResults: 2);
                foreach (var r in results)
                {
                    var content = r.Content.Length > 800 ? r.Content[..800] : r.Content;
                    sb.AppendLine($"[{r.Source}] {r.Title}");
                    sb.AppendLine(content);
                    if (!string.IsNullOrEmpty(r.Url)) sb.AppendLine($"URL: {r.Url}");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BudgetFacts: provider {Provider} query '{Query}' failed", provider, query);
            }
        }

        return sb.ToString();
    }

    private async Task<List<BudgetFact>> SynthesizeContradictionsAsync(
        string apiKey, string rawData, List<string> recentLabels,
        string stateName, int currentFy, int compareFy, CancellationToken ct)
    {
        const string systemPrompt =
            "You are a nonpartisan budget analyst. Your job is to find CONTRADICTIONS in US federal " +
            "budget data — pairs of facts that are BOTH TRUE but create surprising tension when placed " +
            "side by side. A good contradiction is not 'A is false' — it is 'Both A and B are true, but " +
            "they create cognitive dissonance.' Focus on: tax burden vs. effective rates, spending levels " +
            "vs. outcomes, how the same dollar figure looks different depending on framing, or facts that " +
            "challenge common narratives from BOTH left and right. Return ONLY a JSON array, no prose.";

        var dedupBlock = recentLabels.Count > 0
            ? "These contradictions were already shown recently — do NOT repeat them:\n- " +
              string.Join("\n- ", recentLabels) + "\n\n"
            : "";

        var userPrompt =
            $"Today's data focus: {stateName} and the period FY{compareFy}–FY{currentFy}.\n\n" +
            $"Here is today's federal budget data from USASpending.gov and USAFacts.org:\n\n{rawData}\n\n" +
            dedupBlock +
            $"Generate exactly {_factsPerDay} NEW budget contradictions. Prioritize contradictions that " +
            $"involve {stateName} or the {compareFy}→{currentFy} historical change where the data supports it. " +
            "Only state numbers the data above supports or that you are highly confident are accurate. " +
            "Each contradiction must use a DIFFERENT category — do not repeat a category within this batch. " +
            "Never refer to an agency by its numeric code (e.g. 'Agency 028'); use the agency's real name, " +
            "and skip any data point where you don't know the agency's name. Avoid making more than one " +
            "contradiction about budget-authority-vs-obligations gaps; prefer tensions an everyday reader " +
            "would find surprising (who pays, who receives, how framing changes the story).\n" +
            "Return as a JSON array of objects with these exact keys:\n" +
            "[{\"category\": \"Taxation|Defense|Entitlements|Debt|Discretionary\", " +
            "\"tensionLabel\": \"short framing question, max 8 words\", " +
            "\"perspectiveA\": \"the surprising stat, 1-2 sentences with specific numbers\", " +
            "\"sourceA\": \"USASpending.gov or USAFacts\", \"sourceUrlA\": \"url or empty string\", " +
            "\"perspectiveB\": \"the 'but wait...' counter-fact, 1-2 sentences with specific numbers\", " +
            "\"sourceB\": \"USASpending.gov or USAFacts\", \"sourceUrlB\": \"url or empty string\", " +
            "\"explanation\": \"1 sentence on why both are simultaneously true\"}]";

        var model = _config["Anthropic:Model"] ?? "claude-sonnet-4-6";
        var requestBody = new
        {
            model,
            max_tokens = 1500,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } },
        };

        var http = _httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("BudgetFacts: Claude API returned {Status}: {Body}",
                response.StatusCode, json.Length > 500 ? json[..500] : json);
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            return ParseFacts(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BudgetFacts: failed to parse Claude response");
            return [];
        }
    }

    private List<BudgetFact> ParseFacts(string text)
    {
        // Claude may wrap the array in a markdown fence; extract the array bounds
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            _logger.LogWarning("BudgetFacts: no JSON array found in Claude response");
            return [];
        }

        var facts = new List<BudgetFact>();
        using var doc = JsonDocument.Parse(text[start..(end + 1)]);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var fact = new BudgetFact
            {
                Category = GetString(item, "category"),
                TensionLabel = GetString(item, "tensionLabel"),
                PerspectiveA = GetString(item, "perspectiveA"),
                SourceA = GetString(item, "sourceA"),
                SourceUrlA = GetString(item, "sourceUrlA"),
                PerspectiveB = GetString(item, "perspectiveB"),
                SourceB = GetString(item, "sourceB"),
                SourceUrlB = GetString(item, "sourceUrlB"),
                Explanation = GetString(item, "explanation"),
            };

            if (string.IsNullOrWhiteSpace(fact.TensionLabel) ||
                string.IsNullOrWhiteSpace(fact.PerspectiveA) ||
                string.IsNullOrWhiteSpace(fact.PerspectiveB))
            {
                _logger.LogWarning("BudgetFacts: skipping incomplete fact '{Label}'", fact.TensionLabel);
                continue;
            }

            facts.Add(fact);
        }

        return facts;
    }

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
