using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Arena.Shared.Reporting;
using Arena.Shared.Security;
using Civic.API.Services;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Serves Civic's slice of a single UTC day to the Arena backend, which composes it with
/// its own numbers into the operator's daily engagement email. Civic can't send the report
/// itself — the ACS mail path lives in the Arena backend — so this is the seam between them.
///
/// Guards mirror <c>/api/admin/email-smoke</c> in Arena: a shared secret in a header, and
/// nothing in the request influences what data comes back beyond the requested date. The
/// response is COUNTS ONLY, so even a leaked secret exposes no user data. Blank
/// <c>Reporting:Secret</c> disables the endpoint outright.
///
/// Because the caller is a background service rather than a signed-in admin, this can't use
/// the JWT "Admin" policy the engagement dashboard uses.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/admin/daily-stats")]
public class AdminDailyStatsController : ControllerBase
{
    private readonly DailyStatsBuilder _builder;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminDailyStatsController> _logger;

    public AdminDailyStatsController(
        DailyStatsBuilder builder,
        IConfiguration config,
        ILogger<AdminDailyStatsController> logger)
    {
        _builder = builder;
        _config = config;
        _logger = logger;
    }

    /// <param name="date">UTC day to report, as yyyy-MM-dd. Defaults to the most recently
    /// completed UTC day — the same day the Arena report covers.</param>
    [HttpGet]
    public async Task<ActionResult<DailyStatsDto>> Get(
        [FromHeader(Name = "X-Report-Secret")] string? secret,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        var expected = _config["Reporting:Secret"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(503, new { error = "Daily stats endpoint is not configured." });

        if (!SharedSecret.Matches(secret, expected))
        {
            _logger.LogWarning("Rejected daily-stats request with an invalid secret.");
            return Unauthorized(new { error = "Invalid report secret." });
        }

        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParse(date, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return BadRequest(new { error = "Invalid date. Use yyyy-MM-dd." });
            day = parsed;
        }

        return Ok(await _builder.BuildAsync(day, ct));
    }
}
