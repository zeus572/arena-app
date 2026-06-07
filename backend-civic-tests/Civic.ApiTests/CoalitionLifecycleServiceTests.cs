using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Coalition.Product;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// #4 lifecycle automation: deadlines auto-resolve to DIED, the active pool is topped
/// up from unused briefings, and over-tiered players are relegated. Drives the
/// lifecycle service directly against civic_test.
/// </summary>
[Collection("Database")]
public class CoalitionLifecycleServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;
    public CoalitionLifecycleServiceTests(DatabaseFixture fx) => _fx = fx;

    public async Task InitializeAsync() => await _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private (CoalitionLifecycleService svc, CivicDbContext db, IServiceScope scope) Build()
    {
        var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var loop = scope.ServiceProvider.GetRequiredService<CoalitionLoopService>();
        return (new CoalitionLifecycleService(db, loop), db, scope);
    }

    [Fact]
    public async Task ResolveOverdue_DiesProvisionsPastDeadline()
    {
        var id = Guid.NewGuid();
        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            db.Provisions.Add(new Provision
            {
                Id = id, Slug = $"overdue-{id:N}", Title = "Overdue provision",
                NeutralText = "x", State = ProvisionState.Open, Deadline = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var (svc, _, scope) = Build();
        using (scope)
        {
            var resolved = await svc.ResolveOverdueAsync();
            resolved.Should().BeGreaterThanOrEqualTo(1);
        }

        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            (await db.Provisions.SingleAsync(p => p.Id == id)).State.Should().Be(ProvisionState.Died);
        }
    }

    [Fact]
    public async Task TopUp_BirthsProvisionsFromUnusedBriefings()
    {
        var (svc, db, scope) = Build();
        using (scope)
        {
            (await db.Provisions.CountAsync()).Should().Be(0, "mutable tables were reset");
            var born = await svc.TopUpAsync(target: 2);
            born.Should().BeGreaterThanOrEqualTo(1, "there are seeded briefings to birth from");
            (await db.Provisions.CountAsync(p => p.SourceBriefingId != null)).Should().BeGreaterThanOrEqualTo(born);
        }
    }

    [Fact]
    public async Task ComposeLeagues_NeverMixesAgeBands()
    {
        var pid = Guid.NewGuid();
        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            db.Provisions.Add(new Provision { Id = pid, Slug = $"ab-{pid:N}", Title = "x", NeutralText = "x", State = ProvisionState.Open });
            db.CoalitionParticipants.Add(new CoalitionParticipant { Id = Guid.NewGuid(), ProvisionId = pid, UserId = "adult1", SpectrumBucket = "left", AgeBand = "Adult", IsAgent = true });
            db.CoalitionParticipants.Add(new CoalitionParticipant { Id = Guid.NewGuid(), ProvisionId = pid, UserId = "adult2", SpectrumBucket = "right", AgeBand = "Adult", IsAgent = true });
            db.CoalitionParticipants.Add(new CoalitionParticipant { Id = Guid.NewGuid(), ProvisionId = pid, UserId = "minor1", SpectrumBucket = "left", AgeBand = "Minor", IsAgent = false });
            await db.SaveChangesAsync();
        }

        using (var s = _fx.Factory.Services.CreateScope())
        {
            var loop = s.ServiceProvider.GetRequiredService<CoalitionLoopService>();
            await loop.ComposeLeaguesAsync(4);
        }

        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            var members = await db.CoalitionLeagueMembers.ToListAsync();
            members.Should().NotBeEmpty();
            members.GroupBy(m => m.LeagueId)
                .Should().OnlyContain(g => g.Select(m => m.AgeBand).Distinct().Count() == 1,
                    "a league must never mix adults and minors (A8)");
            members.Single(m => m.UserId == "minor1").AgeBand.Should().Be("Minor");
        }
    }

    [Fact]
    public async Task ApplyPromotions_RelegatesAnOverTieredMember()
    {
        var lowId = Guid.NewGuid();
        var highId = Guid.NewGuid();
        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            db.CoalitionLeagues.Add(new CoalitionLeague { Id = lowId, Name = "Low", GapTier = 0.2 });
            db.CoalitionLeagues.Add(new CoalitionLeague { Id = highId, Name = "High", GapTier = 0.9 });
            // A skill-0 player parked in the hardest league should be relegated.
            db.CoalitionLeagueMembers.Add(new CoalitionLeagueMember
            {
                Id = Guid.NewGuid(), LeagueId = highId, UserId = "rookie", SpectrumBucket = "left",
            });
            await db.SaveChangesAsync();
        }

        var (svc, _, scope) = Build();
        using (scope)
        {
            var moved = await svc.ApplyPromotionsAsync();
            moved.Should().BeGreaterThanOrEqualTo(1);
        }

        using (var s = _fx.Factory.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<CivicDbContext>();
            (await db.CoalitionLeagueMembers.SingleAsync(m => m.UserId == "rookie")).LeagueId
                .Should().Be(lowId, "a skill-0 player is relegated out of the hardest league");
        }
    }
}
