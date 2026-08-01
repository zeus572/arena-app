using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Email;
using Arena.Shared.Social;

namespace Arena.API.Services.Reporting;

public enum DailyReportOutcome
{
    Sent,
    /// <summary>A report was already sent for this day — not an error, just nothing to do.</summary>
    AlreadySent,
    /// <summary>Turned off, or no recipient configured.</summary>
    NotConfigured,
    /// <summary>The mail provider rejected the send.</summary>
    Failed,
}

public record DailyReportResult(DailyReportOutcome Outcome, DateOnly Date, string? Subject, string? Detail);

/// <summary>
/// Gathers both apps' numbers, composes the email, sends it, and records that it went — the
/// whole job in one scoped service so the scheduler (<see cref="DailyEngagementReportService"/>)
/// and the manual operator trigger run identical code rather than two drifting copies.
///
/// The send is recorded in EmailSendLogs, which is what makes a restart safe: the App Service
/// can recycle mid-morning without the operator getting the same report twice.
/// </summary>
public class DailyReportSender
{
    private readonly ArenaDbContext _db;
    private readonly ArenaDailyStatsService _stats;
    private readonly CivicStatsClient _civic;
    private readonly DailyReportComposer _composer;
    private readonly IEmailSender _sender;
    private readonly IClock _clock;
    private readonly DailyReportOptions _options;
    private readonly ILogger<DailyReportSender> _logger;

    public DailyReportSender(
        ArenaDbContext db,
        ArenaDailyStatsService stats,
        CivicStatsClient civic,
        DailyReportComposer composer,
        IEmailSender sender,
        IClock clock,
        IOptions<DailyReportOptions> options,
        ILogger<DailyReportSender> logger)
    {
        _db = db;
        _stats = stats;
        _civic = civic;
        _composer = composer;
        _sender = sender;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <param name="date">The UTC day to report on.</param>
    /// <param name="force">Skip the "already sent today" check. Used by the manual trigger so
    /// an operator can always pull a report on demand.</param>
    public async Task<DailyReportResult> SendAsync(DateOnly date, bool force, CancellationToken ct = default)
    {
        var now = _clock.Now.UtcDateTime;
        var recipient = _options.Recipient.Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return new DailyReportResult(DailyReportOutcome.NotConfigured, date, null,
                "DailyReport:Recipient is not set.");
        }

        if (!force && await AlreadySentTodayAsync(now, ct))
        {
            return new DailyReportResult(DailyReportOutcome.AlreadySent, date, null,
                "A daily report has already been sent today.");
        }

        var arena = await _stats.BuildAsync(date, ct);
        var civic = await _civic.TryGetAsync(date, ct);
        var report = await _composer.ComposeAsync(arena, civic, _civic.IsConfigured, ct);

        try
        {
            await _sender.SendAsync(recipient, report.Subject, report.Html, report.Text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately NOT logged to EmailSendLogs: nothing was delivered, so the next tick
            // should retry rather than treat the day as done.
            _logger.LogError(ex, "Daily report send failed for {Date}", date);
            return new DailyReportResult(DailyReportOutcome.Failed, date, report.Subject, ex.Message);
        }

        _db.EmailSendLogs.Add(new EmailSendLog
        {
            Id = Guid.NewGuid(),
            Email = recipient.ToLowerInvariant(),
            Purpose = AccountTokenPurpose.DailyReport,
            SentAt = now,
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Daily report for {Date} sent to {Recipient}", date, recipient);
        return new DailyReportResult(DailyReportOutcome.Sent, date, report.Subject, null);
    }

    /// <summary>
    /// One scheduled report per UTC day, which is the actual product rule — the scheduler only
    /// ever asks for yesterday, so "has a report gone out since midnight?" is the right question
    /// and it needs no covered-date column of its own (EmailSendLog has none, and adding one
    /// would mean a migration for a single boolean's worth of information).
    ///
    /// The consequence, deliberately accepted: an operator who force-sends a report for an older
    /// day has also used up that calendar day's automatic send.
    /// </summary>
    private async Task<bool> AlreadySentTodayAsync(DateTime nowUtc, CancellationToken ct)
    {
        var since = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);

        return await _db.EmailSendLogs.AnyAsync(
            l => l.Purpose == AccountTokenPurpose.DailyReport && l.SentAt >= since, ct);
    }
}
