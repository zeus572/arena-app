using Arena.API.Services.Reporting;
using FluentAssertions;
using Xunit;

namespace Arena.UnitTests.Reporting;

/// <summary>
/// The scheduling decisions in <see cref="DailyEngagementReportService"/>: which day a run
/// covers, and whether the send hour has passed. Both are static so the behaviour that
/// matters — never reporting a partial day, and surviving a restart — is testable without
/// standing up a host.
/// </summary>
public class DailyReportScheduleTests
{
    [Fact]
    public void A_run_always_covers_the_last_complete_utc_day()
    {
        var morningRun = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc);

        DailyEngagementReportService.ReportDateFor(morningRun)
            .Should().Be(new DateOnly(2026, 7, 31),
                "a report sent on the 1st describes the 31st — never a day still in progress");
    }

    [Fact]
    public void Report_day_rolls_back_across_a_month_boundary()
    {
        var justAfterMidnight = new DateTime(2026, 8, 1, 0, 5, 0, DateTimeKind.Utc);

        DailyEngagementReportService.ReportDateFor(justAfterMidnight)
            .Should().Be(new DateOnly(2026, 7, 31));
    }

    [Theory]
    [InlineData(13, 59, false)]
    [InlineData(14, 0, true)]
    [InlineData(14, 30, true)]
    [InlineData(23, 59, true)]
    [InlineData(0, 0, false)]
    public void Send_hour_gate_opens_at_the_configured_hour_and_stays_open(int hour, int minute, bool expected)
    {
        var now = new DateTime(2026, 8, 1, hour, minute, 0, DateTimeKind.Utc);

        // Stays open for the rest of the day on purpose: if the app was down at 14:00, the
        // first tick after it comes back must still send. The durable send log is what
        // prevents a second send, not the clock.
        DailyEngagementReportService.IsPastSendHour(now, hourUtc: 14).Should().Be(expected);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(99)]
    public void Nonsense_hours_are_clamped_rather_than_throwing(int configured)
    {
        var act = () => DailyEngagementReportService.IsPastSendHour(
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc), configured);

        act.Should().NotThrow();
    }

    [Fact]
    public void Hour_zero_means_send_as_soon_after_midnight_as_the_first_tick_runs()
    {
        var justAfterMidnight = new DateTime(2026, 8, 1, 0, 1, 0, DateTimeKind.Utc);

        DailyEngagementReportService.IsPastSendHour(justAfterMidnight, hourUtc: 0).Should().BeTrue();
    }
}
