using Arena.API.Services.Reporting;
using Arena.Shared.Reporting;
using FluentAssertions;
using Xunit;

namespace Arena.UnitTests.Reporting;

/// <summary>
/// Covers <see cref="DailyReportComposer.Render"/> — the pure part of the report. The LLM
/// opener is deliberately an input here, because the report has to be correct and complete
/// whether or not Claude answered.
/// </summary>
public class DailyReportComposerTests
{
    private static readonly DateOnly Day = new(2026, 7, 31);

    private static DailyStatsDto Stats(
        string app,
        int signups = 0,
        int active = 0,
        int activeYesterday = 0,
        int anonymousArrivals = 0,
        params DailyMetricDto[] activities) =>
        new()
        {
            App = app,
            Date = Day,
            GeneratedAt = new DateTime(2026, 8, 1, 14, 0, 0, DateTimeKind.Utc),
            Audience = new DailyAudienceDto
            {
                Signups = signups,
                ActiveUsers = active,
                ActiveUsersYesterday = activeYesterday,
                AnonymousArrivals = anonymousArrivals,
                TotalKnownUsers = 100,
            },
            Activities = activities.ToList(),
        };

    private static DailyMetricDto Metric(string label, int today, int yesterday = 0, double avg7 = 0, int users = 0) =>
        new() { Key = label, Label = label, Area = "People", Today = today, UsersToday = users, Yesterday = yesterday, Avg7 = avg7, Total = today + yesterday };

    [Fact]
    public void Subject_leads_with_the_numbers_that_matter()
    {
        var arena = Stats("arena", signups: 2, active: 5, activities: new[] { Metric("Debate votes cast", 40) });
        var civic = Stats("civic", signups: 1, active: 3, activities: new[] { Metric("Budget exercise run", 2) });

        var report = DailyReportComposer.Render(arena, civic, civicExpected: true, narrative: null);

        report.Subject.Should().Contain("Jul 31");
        report.Subject.Should().Contain("3 signups", "signups are summed across both apps");
        report.Subject.Should().Contain("8 active");
        report.Subject.Should().Contain("42 events");
    }

    [Fact]
    public void Singular_signup_reads_naturally()
    {
        var arena = Stats("arena", signups: 1, activities: new[] { Metric("Debate votes cast", 0) });

        var report = DailyReportComposer.Render(arena, null, civicExpected: false, narrative: null);

        report.Subject.Should().Contain("1 signup,").And.NotContain("1 signups");
    }

    [Fact]
    public void Computed_summary_stands_in_when_there_is_no_narrative()
    {
        var arena = Stats("arena", signups: 0, active: 4, activeYesterday: 2,
            activities: new[] { Metric("Debate votes cast", 12, yesterday: 6, users: 4) });

        var report = DailyReportComposer.Render(arena, null, civicExpected: false, narrative: null);

        report.Text.Should().Contain("No new signups.");
        report.Text.Should().Contain("4 known people active");
        report.Text.Should().Contain("up 2 from yesterday");
        report.Text.Should().Contain("Busiest: debate votes cast (12)");
    }

    [Fact]
    public void Narrative_replaces_the_computed_summary_when_present()
    {
        var arena = Stats("arena", activities: new[] { Metric("Debate votes cast", 3) });

        var report = DailyReportComposer.Render(
            arena, null, civicExpected: false,
            narrative: ("Quiet Friday", "Three votes, no signups, nothing else moved."));

        report.Html.Should().Contain("Quiet Friday");
        report.Text.Should().Contain("Three votes, no signups, nothing else moved.");
        report.Text.Should().NotContain("Busiest:", "the model's summary replaces the computed one");
    }

    [Fact]
    public void Model_written_prose_cannot_inject_markup()
    {
        var arena = Stats("arena", activities: new[] { Metric("Debate votes cast", 1) });

        var report = DailyReportComposer.Render(
            arena, null, civicExpected: false,
            narrative: ("<script>alert(1)</script>", "Ends with <b>bold</b> & an ampersand."));

        report.Html.Should().NotContain("<script>");
        report.Html.Should().Contain("&lt;script&gt;");
        report.Html.Should().Contain("&amp;");
    }

    [Fact]
    public void Activities_that_went_quiet_are_called_out()
    {
        var arena = Stats("arena",
            activities: new[]
            {
                Metric("Debate votes cast", today: 0, yesterday: 5, avg7: 4),
                Metric("Reactions on debates & turns", today: 6, avg7: 5),
                Metric("Winner predictions", today: 0, avg7: 0.1),
            });

        var report = DailyReportComposer.Render(arena, null, civicExpected: false, narrative: null);

        report.Text.Should().Contain("WENT QUIET TODAY");
        report.Text.Should().Contain("Debate votes cast — 0 today, normally 4/day");
        report.Text.Should().NotContain("Winner predictions —",
            "something that averages a tenth of an event a day isn't a signal");
        report.Text.Should().NotContain("Reactions on debates & turns —");
    }

    [Fact]
    public void Missing_civic_stats_are_flagged_not_silently_dropped()
    {
        var arena = Stats("arena", activities: new[] { Metric("Debate votes cast", 1) });

        var flagged = DailyReportComposer.Render(arena, null, civicExpected: true, narrative: null);
        flagged.Html.Should().Contain("Civic stats could not be fetched");
        flagged.Text.Should().Contain("Civic stats could not be fetched");

        var notExpected = DailyReportComposer.Render(arena, null, civicExpected: false, narrative: null);
        notExpected.Html.Should().NotContain("could not be fetched");
        notExpected.Text.Should().NotContain("could not be fetched");
    }

    [Fact]
    public void A_day_with_nothing_at_all_still_renders_a_complete_report()
    {
        var arena = Stats("arena", activities: new[] { Metric("Debate votes cast", 0) });

        var report = DailyReportComposer.Render(arena, null, civicExpected: false, narrative: null);

        report.Subject.Should().Contain("0 signups, 0 active, 0 events");
        report.Text.Should().Contain("No new signups.");
        report.Text.Should().Contain("No activity recorded across either app.");
        report.Html.Should().Contain("Who showed up");
    }

    [Fact]
    public void Both_apps_get_their_own_activity_table()
    {
        var arena = Stats("arena", activities: new[] { Metric("Debate votes cast", 4) });
        var civic = Stats("civic", activities: new[] { Metric("Budget exercise run", 7) });

        var report = DailyReportComposer.Render(arena, civic, civicExpected: true, narrative: null);

        report.Html.Should().Contain("Arena activity").And.Contain("Civic activity");
        report.Text.Should().Contain("ARENA ACTIVITY").And.Contain("CIVIC ACTIVITY");
    }
}
