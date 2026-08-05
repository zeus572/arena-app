using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The six-hour rule. Pure, so the boundary is testable to the minute.
/// </summary>
public class RoomVisibilityTests
{
    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    private static ReviewFlag Flag(TimeSpan age, DateTime? resolvedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = Now - age,
        ResolvedAt = resolvedAt,
    };

    [Fact]
    public void JustInsideSixHours_IsStillVisible()
    {
        RoomVisibility.IsHiddenForReader(Flag(TimeSpan.FromMinutes(359)), Now, null)
            .Should().BeFalse();
    }

    [Fact]
    public void JustPastSixHours_IsHiddenFromANewReader()
    {
        RoomVisibility.IsHiddenForReader(Flag(TimeSpan.FromMinutes(361)), Now, null)
            .Should().BeTrue();
    }

    [Fact]
    public void ExactlySixHours_IsHidden()
    {
        // The boundary is inclusive: "after 6 hours unreviewed" starts AT six hours.
        RoomVisibility.IsHiddenForReader(Flag(TimeSpan.FromHours(6)), Now, null)
            .Should().BeTrue();
    }

    [Fact]
    public void AResolvedFlag_NeverHidesAnything()
    {
        // However old. The point of the rule is unreviewed content, not old content.
        var flag = Flag(TimeSpan.FromDays(30), resolvedAt: Now - TimeSpan.FromDays(29));

        RoomVisibility.IsHiddenForReader(flag, Now, null).Should().BeFalse();
    }

    [Fact]
    public void AReaderAlreadyMidSession_KeepsSeeingIt()
    {
        // Pulling a section out from under someone who started reading before the flag was
        // raised is its own failure — and they have already read it.
        var flag = Flag(TimeSpan.FromHours(7));
        var sessionStartedBeforeTheFlag = Now - TimeSpan.FromHours(8);

        RoomVisibility.IsHiddenForReader(flag, Now, sessionStartedBeforeTheFlag)
            .Should().BeFalse();
    }

    [Fact]
    public void AReaderWhoArrivedAfterTheFlag_DoesNotSeeIt()
    {
        var flag = Flag(TimeSpan.FromHours(7));
        var sessionStartedAfterTheFlag = Now - TimeSpan.FromMinutes(10);

        RoomVisibility.IsHiddenForReader(flag, Now, sessionStartedAfterTheFlag)
            .Should().BeTrue();
    }

    [Fact]
    public void AnonymousReaders_AreAlwaysTreatedAsNew()
    {
        // They get the protection, always. That is the correct default: we cannot prove
        // they were mid-read, and the failure mode of guessing wrong is showing someone
        // content we already know needs fixing.
        RoomVisibility.IsHiddenForReader(Flag(TimeSpan.FromHours(7)), Now, sessionStartedAt: null)
            .Should().BeTrue();
    }

    [Fact]
    public void SessionStart_TreatsARecentVisitAsTheSameSession()
    {
        RoomVisibility.SessionStart(Now - TimeSpan.FromMinutes(20), Now).Should().NotBeNull();
    }

    [Fact]
    public void SessionStart_TreatsALongGapAsANewSession()
    {
        // Someone who wandered off for lunch should not keep seeing content whose grace
        // period expired while they were away.
        RoomVisibility.SessionStart(Now - TimeSpan.FromMinutes(45), Now).Should().BeNull();
        RoomVisibility.SessionStart(null, Now).Should().BeNull();
    }

    [Fact]
    public void Escalation_KicksInAtTwentyFourHours()
    {
        RoomVisibility.NeedsEscalation(Flag(TimeSpan.FromHours(23)), Now).Should().BeFalse();
        RoomVisibility.NeedsEscalation(Flag(TimeSpan.FromHours(25)), Now).Should().BeTrue();
    }

    [Fact]
    public void Escalation_IgnoresResolvedFlags()
    {
        var flag = Flag(TimeSpan.FromDays(5), resolvedAt: Now - TimeSpan.FromDays(4));
        RoomVisibility.NeedsEscalation(flag, Now).Should().BeFalse();
    }

    [Fact]
    public void GraceIsShorterThanEscalation()
    {
        // If these ever crossed, content would be escalated before it was hidden, which is
        // backwards — the hide is the cheap automatic protection, the escalation is the alarm.
        RoomVisibility.UnreviewedGrace.Should().BeLessThan(RoomVisibility.EscalationAfter);
    }
}
