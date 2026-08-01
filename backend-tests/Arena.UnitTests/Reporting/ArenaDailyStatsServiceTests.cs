using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services.Reporting;
using Arena.Shared.Reporting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arena.UnitTests.Reporting;

public class ArenaDailyStatsServiceTests
{
    // The day under report, and the boundaries around it. Everything is UTC — the slice is
    // defined as [day 00:00Z, next day 00:00Z), so the boundary cases below are the ones that
    // decide whether an event lands in "today", "yesterday", or the 7-day baseline.
    private static readonly DateOnly Day = new(2026, 7, 31);
    private static readonly DateTime DayStart = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayEnd = DayStart.AddDays(1);

    private static ArenaDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ArenaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User AddUser(ArenaDbContext db, bool anonymous = false, bool verified = false, DateTime? createdAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = anonymous ? "anon" : "real",
            Email = $"{Guid.NewGuid():N}@example.com",
            IsAnonymous = anonymous,
            EmailVerified = verified,
            CreatedAt = createdAt ?? DayStart.AddHours(3),
        };
        db.Users.Add(user);
        return user;
    }

    private static void AddVote(ArenaDbContext db, User user, DateTime at)
    {
        db.Votes.Add(new Vote
        {
            Id = Guid.NewGuid(),
            DebateId = Guid.NewGuid(),
            UserId = user.Id,
            VotedForAgentId = Guid.NewGuid(),
            CreatedAt = at,
        });
    }

    private static DailyMetricDto Metric(DailyStatsDto stats, string key) =>
        stats.Activities.Single(a => a.Key == key);

    [Fact]
    public async Task Counts_only_events_inside_the_reported_utc_day()
    {
        using var db = NewDb();
        var user = AddUser(db);

        AddVote(db, user, DayStart);                      // first instant of the day — counts
        AddVote(db, user, DayEnd.AddTicks(-1));           // last instant of the day — counts
        AddVote(db, user, DayStart.AddTicks(-1));         // just before midnight — yesterday
        AddVote(db, user, DayEnd);                        // next day — excluded entirely
        await db.SaveChangesAsync();

        var stats = await new ArenaDailyStatsService(db).BuildAsync(Day);
        var votes = Metric(stats, "vote");

        votes.Today.Should().Be(2);
        votes.Yesterday.Should().Be(1);
        // Total is cumulative as of the END of the reported day, so the next-day vote is out.
        votes.Total.Should().Be(3);
    }

    [Fact]
    public async Task Seven_day_average_excludes_the_reported_day()
    {
        using var db = NewDb();
        var user = AddUser(db, createdAt: DayStart.AddDays(-30));

        // 7 events spread over the 7 days before the reported day => average of exactly 1/day.
        for (var i = 1; i <= 7; i++) AddVote(db, user, DayStart.AddDays(-i).AddHours(5));
        // A spike today must not flatten its own baseline.
        for (var i = 0; i < 20; i++) AddVote(db, user, DayStart.AddHours(9));
        await db.SaveChangesAsync();

        var votes = Metric(await new ArenaDailyStatsService(db).BuildAsync(Day), "vote");

        votes.Today.Should().Be(20);
        votes.Avg7.Should().Be(1);
    }

    [Fact]
    public async Task Anonymous_users_are_volume_but_never_active_users()
    {
        using var db = NewDb();
        var known = AddUser(db);
        var anon = AddUser(db, anonymous: true);

        AddVote(db, known, DayStart.AddHours(1));
        AddVote(db, anon, DayStart.AddHours(2));
        AddVote(db, anon, DayStart.AddHours(3));
        await db.SaveChangesAsync();

        var stats = await new ArenaDailyStatsService(db).BuildAsync(Day);

        Metric(stats, "vote").Today.Should().Be(3, "anonymous activity is still activity");
        Metric(stats, "vote").UsersToday.Should().Be(1, "only the known user is a person we can count");
        stats.Audience.ActiveUsers.Should().Be(1);
        stats.Audience.AnonymousEvents.Should().Be(2);
    }

    [Fact]
    public async Task Signups_separate_real_accounts_from_anonymous_arrivals()
    {
        using var db = NewDb();
        AddUser(db, verified: true, createdAt: DayStart.AddHours(2));
        AddUser(db, verified: false, createdAt: DayStart.AddHours(4));
        AddUser(db, anonymous: true, createdAt: DayStart.AddHours(6));
        AddUser(db, createdAt: DayStart.AddDays(-3));    // earlier signup, still a known user
        AddUser(db, createdAt: DayEnd.AddHours(1));      // tomorrow — not counted at all
        await db.SaveChangesAsync();

        var audience = (await new ArenaDailyStatsService(db).BuildAsync(Day)).Audience;

        audience.Signups.Should().Be(2);
        audience.SignupsVerified.Should().Be(1);
        audience.AnonymousArrivals.Should().Be(1);
        audience.SignupsLast7.Should().Be(3);
        audience.TotalKnownUsers.Should().Be(3, "the account created tomorrow doesn't exist yet on this day");
    }

    [Fact]
    public async Task Platform_activity_is_reported_separately_from_people_activity()
    {
        using var db = NewDb();
        var user = AddUser(db);
        var debateId = Guid.NewGuid();

        db.Debates.Add(new Debate
        {
            Id = debateId,
            Topic = "bot debate",
            ProponentId = Guid.NewGuid(),
            OpponentId = Guid.NewGuid(),
            Source = "bot",
            CreatedAt = DayStart.AddHours(1),
        });
        db.Debates.Add(new Debate
        {
            Id = Guid.NewGuid(),
            Topic = "user debate",
            ProponentId = Guid.NewGuid(),
            OpponentId = Guid.NewGuid(),
            Source = "user",
            StartedByUserId = user.Id,
            CreatedAt = DayStart.AddHours(2),
        });
        db.Turns.Add(new Turn
        {
            Id = Guid.NewGuid(),
            DebateId = debateId,
            AgentId = Guid.NewGuid(),
            TurnNumber = 1,
            Content = "argument",
            CreatedAt = DayStart.AddHours(3),
        });
        await db.SaveChangesAsync();

        var stats = await new ArenaDailyStatsService(db).BuildAsync(Day);

        Metric(stats, "debate_created").Today.Should().Be(2);
        Metric(stats, "debate_created").Area.Should().Be(ArenaDailyStatsService.Platform);
        Metric(stats, "turn_generated").Today.Should().Be(1);
        Metric(stats, "debate_started").Today.Should().Be(1, "only one debate has a person behind it");
        Metric(stats, "debate_started").Area.Should().Be(ArenaDailyStatsService.People);

        // Bot volume must not inflate the human numbers.
        stats.Audience.ActiveUsers.Should().Be(1);
    }

    [Fact]
    public async Task Empty_day_reports_zeroes_rather_than_dropping_activities()
    {
        using var db = NewDb();

        var stats = await new ArenaDailyStatsService(db).BuildAsync(Day);

        stats.App.Should().Be("arena");
        stats.Date.Should().Be(Day);
        stats.Activities.Should().NotBeEmpty("a silent day still has to list what was silent");
        stats.Activities.Should().OnlyContain(a => a.Today == 0 && a.Total == 0);
        stats.Audience.Signups.Should().Be(0);
        stats.Audience.ActiveUsers.Should().Be(0);
    }
}
