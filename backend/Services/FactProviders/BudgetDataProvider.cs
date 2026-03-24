using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Arena.API.Services.FactProviders;

public class BudgetDataProvider : IFactProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BudgetDataProvider> _logger;

    private const string BaseUrl = "https://api.usaspending.gov/api/v2";
    private static readonly TimeSpan AgencyCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(12);

    public string Name => "BudgetData";

    // Top ~20 toptier agency codes keyed by common names/abbreviations
    private static readonly Dictionary<string, string> AgencyCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dod"] = "097", ["defense"] = "097", ["military"] = "097", ["pentagon"] = "097",
        ["hhs"] = "075", ["health and human services"] = "075", ["health"] = "075",
        ["education"] = "018", ["doe education"] = "018",
        ["doe"] = "089", ["energy"] = "089",
        ["va"] = "036", ["veterans"] = "036", ["veterans affairs"] = "036",
        ["dhs"] = "070", ["homeland security"] = "070",
        ["hud"] = "086", ["housing"] = "086",
        ["dot"] = "069", ["transportation"] = "069",
        ["doj"] = "015", ["justice"] = "015",
        ["state"] = "019", ["state department"] = "019",
        ["treasury"] = "020",
        ["interior"] = "010",
        ["agriculture"] = "012", ["usda"] = "012",
        ["commerce"] = "013",
        ["labor"] = "016",
        ["epa"] = "068", ["environmental"] = "068",
        ["nasa"] = "080",
        ["ssa"] = "028", ["social security"] = "028",
        ["sba"] = "073", ["small business"] = "073",
    };

    public BudgetDataProvider(HttpClient http, IMemoryCache cache, ILogger<BudgetDataProvider> logger)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ArenaBot/1.0)");
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<FactResult>> SearchAsync(string query, int maxResults = 3)
    {
        var cacheKey = $"budget:search:{ComputeHash(query.Trim().ToLowerInvariant())}";

        if (_cache.TryGetValue(cacheKey, out List<FactResult>? cached) && cached is not null)
        {
            _logger.LogInformation("Budget cache hit for query: {Query}", query);
            return cached;
        }

        var results = new List<FactResult>();

        try
        {
            var agencyCode = MatchAgency(query);

            if (agencyCode is not null)
            {
                var agencyResults = await FetchAgencyBudgetAsync(agencyCode);
                results.AddRange(agencyResults);
            }

            if (HasCategoryKeywords(query))
            {
                var categoryResults = await FetchSpendingByCategoryAsync(query);
                results.AddRange(categoryResults);
            }

            // Always include toptier overview, filtered by query relevance
            if (results.Count < maxResults)
            {
                var overviewResults = await FetchToptierAgenciesAsync(query, maxResults - results.Count);
                results.AddRange(overviewResults);
            }

            results = results.Take(maxResults).ToList();

            _cache.Set(cacheKey, results, new MemoryCacheEntryOptions
            {
                SlidingExpiration = SearchCacheTtl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Budget search failed for query: {Query}", query);
        }

        return results;
    }

    private async Task<List<FactResult>> FetchToptierAgenciesAsync(string query, int maxResults)
    {
        const string cacheKey = "budget:agencies";

        if (!_cache.TryGetValue(cacheKey, out JsonElement agencies))
        {
            try
            {
                var response = await _http.GetAsync($"{BaseUrl}/references/toptier_agencies/");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("USASpending toptier agencies returned {Status}", response.StatusCode);
                    return [];
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                // Clone so it survives document disposal
                agencies = doc.RootElement.GetProperty("results").Clone();
                _cache.Set(cacheKey, agencies, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = AgencyCacheTtl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch toptier agencies");
                return [];
            }
        }

        var queryWords = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var results = new List<(FactResult result, double score)>();

        foreach (var agency in agencies.EnumerateArray())
        {
            var name = agency.GetProperty("agency_name").GetString() ?? "";
            var budgetAuthority = agency.TryGetProperty("budget_authority_amount", out var ba) ? ba.GetDouble() : 0;
            var obligations = agency.TryGetProperty("obligated_amount", out var ob) ? ob.GetDouble() : 0;
            var outlays = agency.TryGetProperty("outlay_amount", out var ol) ? ol.GetDouble() : 0;
            var slug = agency.TryGetProperty("agency_slug", out var s) ? s.GetString() ?? "" : "";

            // Score relevance by keyword match
            var nameLower = name.ToLowerInvariant();
            var matchScore = queryWords.Count(w => nameLower.Contains(w));

            // Give a small score to all agencies so we can fall back to top spenders
            var spendScore = budgetAuthority / 1_000_000_000_000; // normalize to trillions

            results.Add((
                new FactResult
                {
                    Source = "USASpending.gov",
                    Title = $"{name} — Federal Budget Overview",
                    Content = $"Budget Authority: {FormatDollars(budgetAuthority)} | " +
                              $"Obligations: {FormatDollars(obligations)} | " +
                              $"Outlays: {FormatDollars(outlays)}",
                    Url = string.IsNullOrEmpty(slug)
                        ? "https://www.usaspending.gov/explorer"
                        : $"https://www.usaspending.gov/agency/{slug}",
                },
                matchScore * 100 + spendScore
            ));
        }

        return results
            .OrderByDescending(r => r.score)
            .Take(maxResults)
            .Select(r => r.result)
            .ToList();
    }

    private async Task<List<FactResult>> FetchAgencyBudgetAsync(string agencyCode)
    {
        var cacheKey = $"budget:agency:{agencyCode}";

        if (_cache.TryGetValue(cacheKey, out List<FactResult>? cached) && cached is not null)
        {
            return cached;
        }

        var results = new List<FactResult>();

        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/agency/{agencyCode}/budgetary_resources/");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("USASpending agency budget returned {Status} for code {Code}",
                    response.StatusCode, agencyCode);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var agencyName = root.TryGetProperty("agency_data_by_year", out var yearData)
                ? "Agency" : "Agency";

            // Try to get agency name from toptier_code reference
            if (root.TryGetProperty("toptier_code", out _))
            {
                agencyName = $"Agency {agencyCode}";
            }

            if (root.TryGetProperty("agency_data_by_year", out var dataByYear))
            {
                foreach (var yearEntry in dataByYear.EnumerateArray().Take(3))
                {
                    var fiscal_year = yearEntry.TryGetProperty("fiscal_year", out var fy) ? fy.GetInt32() : 0;
                    var budgetAuth = yearEntry.TryGetProperty("agency_budgetary_resources", out var abr)
                        ? abr.GetDouble() : 0;
                    var obligations = yearEntry.TryGetProperty("agency_total_obligated", out var ato)
                        ? ato.GetDouble() : 0;

                    if (fiscal_year == 0) continue;

                    results.Add(new FactResult
                    {
                        Source = "USASpending.gov",
                        Title = $"{agencyName} — FY{fiscal_year} Budget",
                        Content = $"FY{fiscal_year}: Budget Authority: {FormatDollars(budgetAuth)} | " +
                                  $"Total Obligated: {FormatDollars(obligations)}",
                        Url = $"https://www.usaspending.gov/agency/{agencyCode}",
                    });
                }
            }

            _cache.Set(cacheKey, results, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AgencyCacheTtl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch agency budget for code {Code}", agencyCode);
        }

        return results;
    }

    private async Task<List<FactResult>> FetchSpendingByCategoryAsync(string query)
    {
        var results = new List<FactResult>();

        try
        {
            var requestBody = new
            {
                category = "agency",
                filters = new
                {
                    time_period = new[]
                    {
                        new { start_date = $"{DateTime.UtcNow.Year - 1}-10-01", end_date = $"{DateTime.UtcNow.Year}-09-30" }
                    }
                },
                limit = 10,
                page = 1,
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{BaseUrl}/search/spending_by_category/", jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("USASpending spending_by_category returned {Status}", response.StatusCode);
                return results;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("results", out var categoryResults))
            {
                var lines = new List<string>();
                foreach (var item in categoryResults.EnumerateArray().Take(10))
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var amount = item.TryGetProperty("amount", out var a) ? a.GetDouble() : 0;
                    if (!string.IsNullOrEmpty(name) && amount > 0)
                    {
                        lines.Add($"{name}: {FormatDollars(amount)}");
                    }
                }

                if (lines.Count > 0)
                {
                    results.Add(new FactResult
                    {
                        Source = "USASpending.gov",
                        Title = $"Federal Spending by Agency — FY{DateTime.UtcNow.Year}",
                        Content = string.Join(" | ", lines),
                        Url = "https://www.usaspending.gov/explorer/budget_function",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch spending by category");
        }

        return results;
    }

    private static string? MatchAgency(string query)
    {
        var lower = query.ToLowerInvariant();
        foreach (var (keyword, code) in AgencyCodeMap)
        {
            if (lower.Contains(keyword))
                return code;
        }
        return null;
    }

    private static bool HasCategoryKeywords(string query)
    {
        var lower = query.ToLowerInvariant();
        return lower.Contains("by category") || lower.Contains("program") ||
               lower.Contains("cfda") || lower.Contains("all agencies") ||
               lower.Contains("total spending") || lower.Contains("federal spending");
    }

    private static string FormatDollars(double amount)
    {
        return amount switch
        {
            >= 1_000_000_000_000 => $"${amount / 1_000_000_000_000:F2}T",
            >= 1_000_000_000 => $"${amount / 1_000_000_000:F1}B",
            >= 1_000_000 => $"${amount / 1_000_000:F1}M",
            >= 1_000 => $"${amount / 1_000:F0}K",
            _ => $"${amount:F0}",
        };
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}
