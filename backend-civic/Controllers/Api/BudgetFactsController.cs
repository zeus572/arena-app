using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Civic.API.Controllers.Api;

/// <summary>
/// "Did You Know?" budget contradictions. The facts are generated daily by the
/// debate backend (DailyBudgetFactService); this controller proxies them so the
/// civic frontend only ever talks to the civic API. Cached for an hour — the
/// upstream content changes once a day.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/budget-facts")]
public class BudgetFactsController : ControllerBase
{
    private const string CacheKey = "budget-facts:today";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BudgetFactsController> _logger;

    public BudgetFactsController(
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<BudgetFactsController> logger)
    {
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTodaysFacts(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out JsonElement cached))
        {
            return Ok(cached);
        }

        try
        {
            var http = _httpFactory.CreateClient("DebateApi");
            var response = await http.GetAsync("api/budget-facts", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Debate API budget-facts returned {Status}", response.StatusCode);
                return Ok(Array.Empty<object>());
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var facts = doc.RootElement.Clone();

            _cache.Set(CacheKey, facts, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
            });

            return Ok(facts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The card section simply doesn't render when no facts are available,
            // so degrade to an empty list rather than surfacing an error.
            _logger.LogWarning(ex, "Failed to fetch budget facts from debate API");
            return Ok(Array.Empty<object>());
        }
    }
}
