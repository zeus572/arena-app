using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Arena.API.Services.Reporting;
using Arena.Shared.Reporting;
using Arena.Shared.Security;

namespace Arena.API.Controllers.Api;

/// <summary>
/// Operator access to the daily engagement report: read Arena's raw day slice, or send the
/// report on demand instead of waiting for the scheduled hour.
///
/// Guards mirror <c>/api/admin/email-smoke</c>: a shared secret in a header, and the recipient
/// comes only from server config — the request can't redirect where mail goes, so this can't
/// be used as a relay. Blank <c>DailyReport:Secret</c> disables both endpoints.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminDailyReportController : ControllerBase
{
    private readonly ArenaDailyStatsService _stats;
    private readonly DailyReportSender _sender;
    private readonly DailyReportOptions _options;
    private readonly ILogger<AdminDailyReportController> _logger;

    public AdminDailyReportController(
        ArenaDailyStatsService stats,
        DailyReportSender sender,
        IOptions<DailyReportOptions> options,
        ILogger<AdminDailyReportController> logger)
    {
        _stats = stats;
        _sender = sender;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Arena's own day slice — the same data the email is built from, for debugging
    /// a number that looks wrong.</summary>
    [HttpGet("daily-stats")]
    public async Task<ActionResult<DailyStatsDto>> GetStats(
        [FromHeader(Name = "X-Report-Secret")] string? secret,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        if (!Authorized(secret, out var problem)) return problem!;
        if (!TryResolveDate(date, out var day)) return BadRequest(new { error = "Invalid date. Use yyyy-MM-dd." });

        return Ok(await _stats.BuildAsync(day, ct));
    }

    /// <summary>Compose and send the report now, bypassing the once-a-day guard.</summary>
    [HttpPost("daily-report/send")]
    public async Task<IActionResult> Send(
        [FromHeader(Name = "X-Report-Secret")] string? secret,
        [FromQuery] string? date,
        CancellationToken ct)
    {
        if (!Authorized(secret, out var problem)) return problem!;
        if (!TryResolveDate(date, out var day)) return BadRequest(new { error = "Invalid date. Use yyyy-MM-dd." });

        var result = await _sender.SendAsync(day, force: true, ct);

        return result.Outcome switch
        {
            DailyReportOutcome.Sent => Ok(new { status = "sent", date = day.ToString("yyyy-MM-dd"), subject = result.Subject }),
            DailyReportOutcome.NotConfigured => StatusCode(503, new { error = result.Detail }),
            DailyReportOutcome.Failed => StatusCode(502, new { error = "Send failed.", detail = result.Detail }),
            _ => Ok(new { status = result.Outcome.ToString(), date = day.ToString("yyyy-MM-dd"), detail = result.Detail }),
        };
    }

    private bool Authorized(string? secret, out ActionResult? problem)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            problem = StatusCode(503, new { error = "Daily report endpoints are not configured." });
            return false;
        }

        if (!SharedSecret.Matches(secret, _options.Secret))
        {
            _logger.LogWarning("Rejected daily-report request with an invalid secret.");
            problem = Unauthorized(new { error = "Invalid report secret." });
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>Defaults to the most recently completed UTC day — what the scheduled send covers.</summary>
    private static bool TryResolveDate(string? date, out DateOnly day)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            day = DailyEngagementReportService.ReportDateFor(DateTime.UtcNow);
            return true;
        }

        return DateOnly.TryParse(date, System.Globalization.CultureInfo.InvariantCulture, out day);
    }
}
