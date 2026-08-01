using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Arena.Shared.Reporting;

namespace Arena.API.Services.Reporting;

/// <summary>
/// Fetches Civic's day slice from its operator endpoint. Returns null on any failure —
/// unreachable, unauthorized, malformed — because a broken Civic call must not stop the
/// Arena report from going out. The composer renders an explicit "Civic stats unavailable"
/// line in that case, so a missing app is visible rather than silently absent.
/// </summary>
public class CivicStatsClient
{
    private readonly HttpClient _http;
    private readonly DailyReportOptions _options;
    private readonly ILogger<CivicStatsClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CivicStatsClient(
        HttpClient http,
        IOptions<DailyReportOptions> options,
        ILogger<CivicStatsClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>True when a base URL and secret are both configured — i.e. the Civic section
    /// is expected in the report at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.CivicBaseUrl) && !string.IsNullOrWhiteSpace(_options.CivicSecret);

    public async Task<DailyStatsDto?> TryGetAsync(DateOnly date, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Daily report: Civic stats not configured, skipping that section.");
            return null;
        }

        var url = $"{_options.CivicBaseUrl.TrimEnd('/')}/api/admin/daily-stats?date={date:yyyy-MM-dd}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Report-Secret", _options.CivicSecret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Daily report: Civic stats returned {Status}: {Body}",
                    (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
                return null;
            }

            return JsonSerializer.Deserialize<DailyStatsDto>(body, JsonOpts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Daily report: failed to fetch Civic stats from {Url}", url);
            return null;
        }
    }
}
