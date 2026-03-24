using Arena.API.Services.FactProviders;

namespace Arena.API.Services;

public class FactCheckService
{
    private readonly IEnumerable<IFactProvider> _providers;
    private readonly ILogger<FactCheckService> _logger;

    public FactCheckService(IEnumerable<IFactProvider> providers, ILogger<FactCheckService> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Search a specific provider by name.
    /// </summary>
    public async Task<List<FactResult>> SearchProviderAsync(string providerName, string query, int maxResults = 3)
    {
        var provider = _providers.FirstOrDefault(p =>
            p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            _logger.LogWarning("Fact provider '{Provider}' not found", providerName);
            return new List<FactResult>();
        }

        _logger.LogInformation("Fact check: [{Provider}] searching for '{Query}'", providerName, query);
        return await provider.SearchAsync(query, maxResults);
    }

    /// <summary>
    /// Search across all providers and merge results.
    /// </summary>
    public async Task<List<FactResult>> SearchAllAsync(string query, int maxResultsPerProvider = 2)
    {
        var tasks = _providers.Select(p => p.SearchAsync(query, maxResultsPerProvider));
        var allResults = await Task.WhenAll(tasks);
        return allResults.SelectMany(r => r).ToList();
    }

    /// <summary>
    /// Returns the tool definitions for Claude's tool-use API.
    /// </summary>
    public static List<object> GetToolDefinitions()
    {
        return new List<object>
        {
            new
            {
                name = "search_usafacts",
                description = "Search USAFacts.org for verified US government data, statistics, and facts. Use this for claims about US government spending, demographics, economic data, education stats, healthcare stats, crime stats, and other quantitative claims about the United States.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The search query for finding relevant facts and statistics"
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "search_wikipedia",
                description = "Search Wikipedia for background information, historical context, definitions, and general knowledge. Use this for understanding concepts, historical events, policy background, and general factual claims.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The search query for finding relevant Wikipedia articles"
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "search_web",
                description = "Search the web for recent news, studies, reports, and other evidence. Use this for current events, recent research findings, news reports, and claims that need up-to-date sources.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The search query"
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "search_budget",
                description = "Search federal budget and spending data from USASpending.gov. Use this for claims about government spending, agency budgets, federal outlays, appropriations, budget authority, and fiscal policy. Returns real budget figures from official US government sources.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "The budget or spending query, e.g. 'Department of Defense budget 2024' or 'federal spending on education'"
                        }
                    },
                    required = new[] { "query" }
                }
            }
        };
    }
}
