using System.Net;
using System.Net.Http.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class CohortControllerTests
{
    private readonly DatabaseFixture _fixture;

    public CohortControllerTests(DatabaseFixture fixture) => _fixture = fixture;

    private HttpClient ClientFor(string userId)
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    private async Task WithDb(Func<CivicDbContext, Task> work)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        await work(db);
    }

    [Fact]
    public void WeekOf_ReturnsMondayKey()
    {
        // Wednesday 2026-06-10 → Monday 2026-06-08.
        var (key, start) = CohortService.WeekOf(new DateTime(2026, 6, 10, 13, 0, 0, DateTimeKind.Utc));
        key.Should().Be("2026-06-08");
        start.Should().Be(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Me_ReturnsCohortWithCaller_AndIsStableAcrossCalls()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var first = await client.GetFromJsonAsync<CohortDto>("/api/cohort/me");
        first.Should().NotBeNull();
        first!.TargetSize.Should().Be(50);
        first.MemberCount.Should().BeGreaterThanOrEqualTo(1);
        first.WeekKey.Should().Be(CohortService.WeekOf(DateTime.UtcNow).Key);
        first.Leaderboard.Should().Contain(s => s.IsMe);
        first.YourRank.Should().BeGreaterThanOrEqualTo(1);

        // Assignment is idempotent: a second call returns the same cohort.
        var second = await client.GetFromJsonAsync<CohortDto>("/api/cohort/me");
        second!.CohortId.Should().Be(first.CohortId);
    }

    [Fact]
    public async Task Me_SeedsFromLeagueFriends()
    {
        await _fixture.ResetMutableAsync();
        var owner = Guid.NewGuid().ToString();
        var friend = Guid.NewGuid().ToString();
        var leagueId = Guid.NewGuid();

        await WithDb(async db =>
        {
            db.Leagues.Add(new League { Id = leagueId, OwnerUserId = owner, Name = "Test Crew" });
            db.LeagueMembers.Add(new LeagueMember { Id = Guid.NewGuid(), LeagueId = leagueId, UserId = owner, Role = LeagueMemberRole.Owner, DisplayName = "Owner" });
            db.LeagueMembers.Add(new LeagueMember { Id = Guid.NewGuid(), LeagueId = leagueId, UserId = friend, Role = LeagueMemberRole.Member, DisplayName = "Friend" });
            await db.SaveChangesAsync();
        });

        var dto = await ClientFor(owner).GetFromJsonAsync<CohortDto>("/api/cohort/me");
        dto.Should().NotBeNull();
        dto!.LeagueName.Should().Be("Test Crew");
        dto.FriendsCount.Should().BeGreaterThanOrEqualTo(2);
        dto.Leaderboard.Should().Contain(s => s.UserId == friend && s.IsFriend);
        dto.Leaderboard.Should().Contain(s => s.UserId == owner && s.IsMe);
    }

    [Fact]
    public async Task Me_WeeklyPointsReflectCoalitionActs()
    {
        await _fixture.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        // Create the cohort first so the user is a member.
        await client.GetFromJsonAsync<CohortDto>("/api/cohort/me");

        await WithDb(async db =>
        {
            db.CoalitionActs.Add(new CoalitionAct
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = CoalitionActType.Position,
                Points = 7,
                Currency = "reasoning",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var dto = await client.GetFromJsonAsync<CohortDto>("/api/cohort/me");
        dto!.YourWeeklyPoints.Should().BeGreaterThanOrEqualTo(7);
        dto.Leaderboard.First(s => s.IsMe).WeeklyPoints.Should().BeGreaterThanOrEqualTo(7);
    }
}
