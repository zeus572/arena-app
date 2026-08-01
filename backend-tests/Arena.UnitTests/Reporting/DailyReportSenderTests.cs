using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Email;
using Arena.API.Services.Reporting;
using Arena.Shared.Llm;
using Arena.Shared.Social;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.UnitTests.Reporting;

/// <summary>
/// The once-a-day guarantee. An App Service recycle mid-morning must not produce a second
/// copy of the same report, and a provider failure must NOT be recorded as a send — otherwise
/// the retry on the next tick would be skipped and the day would silently go unreported.
/// </summary>
public class DailyReportSenderTests
{
    private static readonly DateOnly Day = new(2026, 7, 31);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = new();
        public bool ThrowOnSend { get; set; }

        public Task SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct = default)
        {
            if (ThrowOnSend) throw new InvalidOperationException("provider rejected the message");
            Sent.Add((toAddress, subject));
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for Anthropic being off/keyless — the composer must fall back.</summary>
    private sealed class UnavailableLlmClient : ILlmClient
    {
        public Task<T> GenerateStructuredAsync<T>(
            string systemPrompt,
            string userPrompt,
            LlmModelTier tier = LlmModelTier.Sonnet,
            int? maxTokens = null,
            CancellationToken ct = default) =>
            throw new LlmException("Anthropic LLM is disabled (test).");
    }

    /// <summary>Wall clock the test drives — the "already sent today" rule is defined against it.</summary>
    private sealed class TestClock : IClock
    {
        public DateTimeOffset Now { get; set; } =
            new(2026, 8, 1, 14, 0, 0, TimeSpan.Zero);   // the morning after Day
    }

    private static ArenaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArenaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (DailyReportSender sender, RecordingEmailSender mail, TestClock clock) Build(
        ArenaDbContext db, string recipient = "ops@example.com")
    {
        var options = Options.Create(new DailyReportOptions
        {
            Enabled = true,
            Recipient = recipient,
            // Blank Civic config: the client short-circuits without making a request, and the
            // composer renders an Arena-only report.
            CivicBaseUrl = "",
            CivicSecret = "",
        });

        var mail = new RecordingEmailSender();
        var composer = new DailyReportComposer(
            new UnavailableLlmClient(), options, NullLogger<DailyReportComposer>.Instance);
        var civic = new CivicStatsClient(
            new HttpClient(), options, NullLogger<CivicStatsClient>.Instance);

        var clock = new TestClock();
        var sender = new DailyReportSender(
            db,
            new ArenaDailyStatsService(db),
            civic,
            composer,
            mail,
            clock,
            options,
            NullLogger<DailyReportSender>.Instance);

        return (sender, mail, clock);
    }

    [Fact]
    public async Task Sends_the_report_and_records_that_it_went()
    {
        using var db = NewDb();
        var (sender, mail, _) = Build(db);

        var result = await sender.SendAsync(Day, force: false);

        result.Outcome.Should().Be(DailyReportOutcome.Sent);
        mail.Sent.Should().ContainSingle();
        mail.Sent[0].To.Should().Be("ops@example.com");

        var log = await db.EmailSendLogs.SingleAsync();
        log.Purpose.Should().Be(AccountTokenPurpose.DailyReport);
        log.Email.Should().Be("ops@example.com");
    }

    [Fact]
    public async Task A_restart_on_the_same_day_does_not_send_a_second_copy()
    {
        using var db = NewDb();
        var (sender, mail, _) = Build(db);

        await sender.SendAsync(Day, force: false);
        var second = await sender.SendAsync(Day, force: false);

        second.Outcome.Should().Be(DailyReportOutcome.AlreadySent);
        mail.Sent.Should().ContainSingle("the durable send log is what survives a restart");
    }

    [Fact]
    public async Task The_manual_trigger_can_always_send()
    {
        using var db = NewDb();
        var (sender, mail, _) = Build(db);

        await sender.SendAsync(Day, force: false);
        var forced = await sender.SendAsync(Day, force: true);

        forced.Outcome.Should().Be(DailyReportOutcome.Sent);
        mail.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_next_day_gets_its_own_report()
    {
        using var db = NewDb();
        var (sender, mail, clock) = Build(db);

        await sender.SendAsync(Day, force: false);

        clock.Now = clock.Now.AddDays(1);
        var tomorrow = await sender.SendAsync(Day.AddDays(1), force: false);

        tomorrow.Outcome.Should().Be(DailyReportOutcome.Sent);
        mail.Sent.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_failed_send_is_not_recorded_so_the_next_tick_retries()
    {
        using var db = NewDb();
        var (sender, mail, _) = Build(db);
        mail.ThrowOnSend = true;

        var failed = await sender.SendAsync(Day, force: false);

        failed.Outcome.Should().Be(DailyReportOutcome.Failed);
        (await db.EmailSendLogs.CountAsync()).Should().Be(0);

        // Next tick: the provider recovers and the day still gets reported.
        mail.ThrowOnSend = false;
        (await sender.SendAsync(Day, force: false)).Outcome.Should().Be(DailyReportOutcome.Sent);
        mail.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Without_a_recipient_nothing_is_sent()
    {
        using var db = NewDb();
        var (sender, mail, _) = Build(db, recipient: "   ");

        var result = await sender.SendAsync(Day, force: true);

        result.Outcome.Should().Be(DailyReportOutcome.NotConfigured);
        mail.Sent.Should().BeEmpty();
    }

}
