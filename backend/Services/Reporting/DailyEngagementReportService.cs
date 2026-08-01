using Microsoft.Extensions.Options;

namespace Arena.API.Services.Reporting;

/// <summary>
/// Sends the operator's daily engagement email once per UTC day, at or after
/// <c>DailyReport:HourUtc</c>.
///
/// Scheduling is a poll plus a durable "did it already go?" check rather than a sleep-until
/// timer, which is what makes it survive the way App Service actually behaves: a deploy or
/// recycle at 13:59 doesn't lose the day's report, because the next tick after boot sees that
/// the hour has passed and nothing has been sent yet. The flip side — no report at all when the
/// app is asleep all day — is accepted; this is an operator digest, not an alerting path.
///
/// The report always covers the most recently COMPLETED UTC day, so a morning email describes
/// a full 24 hours instead of a partial one.
/// </summary>
public class DailyEngagementReportService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DailyReportOptions _options;
    private readonly ILogger<DailyEngagementReportService> _logger;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);

    public DailyEngagementReportService(
        IServiceScopeFactory scopeFactory,
        IOptions<DailyReportOptions> options,
        ILogger<DailyEngagementReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The day a report generated at <paramref name="utcNow"/> covers.</summary>
    public static DateOnly ReportDateFor(DateTime utcNow) => DateOnly.FromDateTime(utcNow).AddDays(-1);

    /// <summary>Whether the configured send hour has passed on the current UTC day. Stays true
    /// for the rest of the day on purpose — the durable send log, not the clock, is what stops
    /// a second send.</summary>
    public static bool IsPastSendHour(DateTime utcNow, int hourUtc) => utcNow.Hour >= Math.Clamp(hourUtc, 0, 23);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DailyEngagementReportService disabled via DailyReport:Enabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.Recipient))
        {
            _logger.LogWarning("DailyReport:Enabled is true but DailyReport:Recipient is empty — not starting.");
            return;
        }

        _logger.LogInformation(
            "DailyEngagementReportService started. Sends after {Hour:00}:00 UTC to {Recipient}.",
            Math.Clamp(_options.HourUtc, 0, 23), _options.Recipient);

        using (var readyScope = _scopeFactory.CreateScope())
            await readyScope.ServiceProvider.GetRequiredService<StartupReadiness>()
                .WaitUntilReadyAsync(stoppingToken);

        // Stagger from the other background services' initial delays.
        await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (IsPastSendHour(now, _options.HourUtc))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<DailyReportSender>();
                    var result = await sender.SendAsync(ReportDateFor(now), force: false, stoppingToken);

                    if (result.Outcome == DailyReportOutcome.Failed)
                        _logger.LogError("Daily report for {Date} failed: {Detail}", result.Date, result.Detail);
                    else if (result.Outcome == DailyReportOutcome.Sent)
                        _logger.LogInformation("Daily report for {Date} sent.", result.Date);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Daily report tick failed");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
