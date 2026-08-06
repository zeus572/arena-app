using System.Net.Http.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The hand-authored pilot room, seeded against a real database.
///
/// The suite runs with Rooms:SeedPilot off, so these tests drive
/// <see cref="RoomSeeder.SeedFileAsync"/> directly — which also proves the seeder works
/// when called with a file rather than only off embedded resources.
/// </summary>
[Collection("Database")]
public class RoomSeederTests
{
    private readonly DatabaseFixture _fx;

    public RoomSeederTests(DatabaseFixture fx) => _fx = fx;

    private static RoomSeedFile PilotFile()
        => SeedService.LoadJson<RoomSeedFile>("Seed.rooms.federal-appropriations.json")!;

    private async Task SeedAsync(RoomStatus status = RoomStatus.Published)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<RoomSeeder>();
        await seeder.SeedFileAsync(PilotFile(), status);
    }

    private static async Task<T> QueryAsync<T>(
        DatabaseFixture fx, Func<CivicDbContext, Task<T>> query)
    {
        using var scope = fx.Factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CivicDbContext>());
    }

    [Fact]
    public async Task Seeding_CreatesTheThemeRoomAndItsGraph()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var (room, claims, actors, timeline) = await QueryAsync(_fx, async db => (
            await db.ThemeRooms.FirstOrDefaultAsync(r => r.Slug == "federal-appropriations"),
            await db.Claims.CountAsync(),
            await db.Actors.CountAsync(),
            await db.TimelineEvents.CountAsync()));

        room.Should().NotBeNull();
        room!.EssentialFacts.Should().HaveCount(3, "the front door shows exactly three");
        room.MatchTerms.Should().NotBeEmpty("the candidate pass matches on these");
        claims.Should().BeGreaterThan(0);
        actors.Should().BeGreaterThan(0);
        timeline.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Seeding_IsIdempotent()
    {
        // It runs on every startup behind a flag, so a second pass must change nothing.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var first = await QueryAsync(_fx, async db => (
            Rooms: await db.Rooms.CountAsync(),
            Claims: await db.Claims.CountAsync(),
            Actors: await db.Actors.CountAsync(),
            Links: await db.ObjectLinks.CountAsync(),
            Timeline: await db.TimelineEvents.CountAsync(),
            History: await db.ClaimStatusHistories.CountAsync()));

        await SeedAsync();

        var second = await QueryAsync(_fx, async db => (
            Rooms: await db.Rooms.CountAsync(),
            Claims: await db.Claims.CountAsync(),
            Actors: await db.Actors.CountAsync(),
            Links: await db.ObjectLinks.CountAsync(),
            Timeline: await db.TimelineEvents.CountAsync(),
            History: await db.ClaimStatusHistories.CountAsync()));

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task EssentialFacts_ResolveToRealClaims()
    {
        // A slug typo here would render the front door with no evidence marks and no error.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var resolved = await QueryAsync(_fx, async db =>
        {
            var room = await db.ThemeRooms.FirstAsync(r => r.Slug == "federal-appropriations");
            var ids = room.EssentialFacts.Select(f => f.ClaimId).ToList();
            return ids.All(id => id is not null)
                && await db.Claims.CountAsync(c => ids.Contains(c.Id)) == ids.Count;
        });

        resolved.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimEvidence_IsAttachedAsGraphEdges()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var supported = await QueryAsync(_fx, db => db.ObjectLinks.CountAsync(
            l => l.FromType == ObjectType.Claim
              && l.Relation == LinkRelation.SupportedBy
              && l.ToType == ObjectType.SourceRef
              && l.ValidTo == null));

        supported.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EveryClaim_StartsItsStatusHistory()
    {
        // Design 1n gives "history of this label" its own cell; it must never be empty.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var orphaned = await QueryAsync(_fx, db => db.Claims
            .Where(c => !db.ClaimStatusHistories.Any(h => h.ClaimId == c.Id))
            .CountAsync());

        orphaned.Should().Be(0);
    }

    [Fact]
    public async Task ActorRoles_AreTieredForTheRoom()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var tiers = await QueryAsync(_fx, async db =>
        {
            var room = await db.ThemeRooms.FirstAsync(r => r.Slug == "federal-appropriations");
            return await db.ActorRoomRoles
                .Where(r => r.RoomId == room.Id)
                .Select(r => r.Tier)
                .ToListAsync();
        });

        tiers.Should().NotBeEmpty();
        tiers.Should().Contain(ActorTier.Decides);
        tiers.Should().Contain(ActorTier.Constrained);
    }

    [Fact]
    public async Task ActorRoomRole_RejectsADuplicateDefaultTiering()
    {
        // The Postgres NULL-distinct trap: DecisionKey is null for the default tiering, and
        // a single unique index would happily accept two default roles for the same actor.
        // The split filtered index is what stops that.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var existing = await db.ActorRoomRoles.FirstAsync(r => r.DecisionKey == null);

        db.ActorRoomRoles.Add(new ActorRoomRole
        {
            Id = Guid.NewGuid(),
            ActorId = existing.ActorId,
            RoomId = existing.RoomId,
            DecisionKey = null,
            Tier = ActorTier.Shapes,
        });

        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ActorRoomRole_AllowsASecondTieringForANamedDecision()
    {
        // The other half of the split index: the same actor can be tiered differently
        // relative to a specific decision. That is what makes design 1i's re-sort possible.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var existing = await db.ActorRoomRoles.FirstAsync(r => r.DecisionKey == null);

        // Seeded actors may already carry decision-scoped rows, so count rather than assume.
        var before = await db.ActorRoomRoles.CountAsync(
            r => r.ActorId == existing.ActorId && r.RoomId == existing.RoomId);

        db.ActorRoomRoles.Add(new ActorRoomRole
        {
            Id = Guid.NewGuid(),
            ActorId = existing.ActorId,
            RoomId = existing.RoomId,
            DecisionKey = "senate-cloture-vote",
            Tier = ActorTier.Constrained,
            LeverageStatement = "No procedural role in this particular vote.",
        });

        await db.SaveChangesAsync();

        (await db.ActorRoomRoles.CountAsync(
                r => r.ActorId == existing.ActorId && r.RoomId == existing.RoomId))
            .Should().Be(before + 1);

        // The default tiering survives alongside it — that is the point of the split index.
        (await db.ActorRoomRoles.CountAsync(
                r => r.ActorId == existing.ActorId && r.RoomId == existing.RoomId
                  && r.DecisionKey == null))
            .Should().Be(1);
    }

    [Fact]
    public async Task Concepts_AreExtendedNotForked()
    {
        // The knowledge item IS the Concept table. If a parallel table ever appears, this
        // is where it shows up — the pilot's concepts must be reachable through /api/concepts.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var concept = await QueryAsync(_fx, db =>
            db.Concepts.FirstOrDefaultAsync(c => c.Slug == "appropriation"));

        concept.Should().NotBeNull();
        concept!.KnowledgeKind.Should().Be(KnowledgeKind.Process);
        concept.ShortGloss.Should().NotBeNullOrWhiteSpace("design 1h's glossary grid needs it");
        concept.ConfusionPairSlug.Should().Be("authorization");

        var res = await _fx.Factory.CreateClient().GetAsync("/api/concepts/appropriation");
        res.IsSuccessStatusCode.Should().BeTrue("the existing concepts API must still serve it");
    }

    [Fact]
    public async Task SeededRoom_LandsAtTheConfiguredStatus()
    {
        // Production seeds at Draft. A seeded room is a fixture, not published content.
        await _fx.ResetMutableAsync();
        await SeedAsync(RoomStatus.Draft);

        var status = await QueryAsync(_fx, db => db.Rooms
            .Where(r => r.Slug == "federal-appropriations")
            .Select(r => r.Status)
            .FirstAsync());

        status.Should().Be(RoomStatus.Draft);

        var res = await _fx.Factory.CreateClient().GetAsync("/api/rooms/federal-appropriations");
        res.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound,
            "a Draft room is not served publicly");
    }

    [Fact]
    public async Task SeededContent_IsStampedAsSeedProvenance()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var (room, claim) = await QueryAsync(_fx, async db => (
            await db.ThemeRooms.FirstAsync(r => r.Slug == "federal-appropriations"),
            await db.Claims.FirstAsync()));

        room.GenerationSource.Should().Be(CivicGenerationSource.Seed);
        room.Provenance.Should().OnlyContain(p => p.ProposedBy == ProvenanceOrigin.Seed);
        claim.GenerationSource.Should().Be(CivicGenerationSource.Seed);
    }

    // ---------------------------------------------------------------- section endpoints

    [Fact]
    public async Task ActorsEndpoint_TiersThePilotRoom()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var dto = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<RoomActorsDto>("/api/rooms/federal-appropriations/actors");

        dto!.Decides.Should().NotBeEmpty();
        dto.Constrained.Should().NotBeEmpty();
        dto.Decides.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.LeverageStatement));
    }

    [Fact]
    public async Task ActorsEndpoint_FallsBackToDefaultTieringForAnUnknownDecision()
    {
        // A re-sort that silently dropped actors would misrepresent who is involved, so
        // actors with no role for the requested decision keep their default row.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var client = _fx.Factory.CreateClient();
        var byDefault = await client.GetFromJsonAsync<RoomActorsDto>(
            "/api/rooms/federal-appropriations/actors");
        var byDecision = await client.GetFromJsonAsync<RoomActorsDto>(
            "/api/rooms/federal-appropriations/actors?decision=nonexistent-vote");

        var defaultCount = byDefault!.Decides.Count + byDefault.Shapes.Count + byDefault.Constrained.Count;
        var decisionCount = byDecision!.Decides.Count + byDecision.Shapes.Count + byDecision.Constrained.Count;

        decisionCount.Should().Be(defaultCount);
    }

    [Fact]
    public async Task TimelineEndpoint_ReturnsEventsWithTheirTextAlternatives()
    {
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var events = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<List<TimelineEventDto>>("/api/rooms/federal-appropriations/timeline");

        events.Should().NotBeEmpty();
        events!.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.TextAlternative));
        events.Should().Contain(e => e.Marker == "Now");
    }

    [Fact]
    public async Task ChoosingADecision_ActuallyRetiersTheMap()
    {
        // Design 1i's premise is that leverage belongs to an actor AND a decision, not to an
        // actor. The selector is rendered whenever availableDecisions is non-empty, so if
        // every decision-scoped role carries the same tier as its default the reader changes
        // the dropdown and nothing moves — a visible control with no visible effect.
        //
        // This asserts the pilot has at least one actor whose tier genuinely differs under a
        // named decision, and that every actor still appears (the filter re-sorts, never hides).
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var client = _fx.Factory.CreateClient();
        var baseline = await client.GetFromJsonAsync<RoomActorsDto>(
            "/api/rooms/federal-appropriations/actors");

        baseline!.AvailableDecisions.Should().NotBeEmpty();

        static Dictionary<string, string> Tiers(RoomActorsDto d) =>
            d.Decides.Concat(d.Shapes).Concat(d.Constrained)
                .ToDictionary(a => a.Slug, a => a.Tier);

        var before = Tiers(baseline);
        var moved = 0;

        foreach (var decision in baseline.AvailableDecisions)
        {
            var filtered = await client.GetFromJsonAsync<RoomActorsDto>(
                $"/api/rooms/federal-appropriations/actors?decision={decision}");

            var after = Tiers(filtered!);
            after.Keys.Should().BeEquivalentTo(before.Keys,
                "choosing a decision re-sorts the map, it never removes anyone from it");

            moved += after.Count(kv => before[kv.Key] != kv.Value);
        }

        moved.Should().BeGreaterThan(0,
            "at least one actor must hold different leverage over a named decision than over "
          + "the room as a whole, or the decision selector does nothing");
    }

    [Fact]
    public async Task LatestEndpoint_ReportsWhatItLoggedAndWhatItLeftOut()
    {
        // Design 1g prints "we logged N articles and judged M of them to have changed
        // something", so the endpoint has to return both halves and they have to add up.
        // ExcludedCount is computed as N − M and clamps at zero, which means an inflated M
        // would render as "0 excluded" instead of failing — hence the explicit check.
        await _fx.ResetMutableAsync();
        await SeedAsync();

        var latest = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<RoomLatestDto>("/api/rooms/federal-appropriations/latest");

        latest!.Developments.Should().NotBeEmpty();
        latest.ArticlesConsidered.Should().BeGreaterThanOrEqualTo(latest.Developments.Count);
        latest.ExcludedCount.Should().Be(latest.ArticlesConsidered - latest.Developments.Count);
        latest.InclusionRules.Should().NotBeEmpty("the rule is printed beside the list");

        // Every row must be able to name the clause that let it in — that disclosure is the
        // only thing that makes the excluded count meaningful rather than decorative.
        latest.Developments.Should().OnlyContain(d =>
            !string.IsNullOrWhiteSpace(d.InclusionReason) &&
            !string.IsNullOrWhiteSpace(d.WhyItMatters));
    }
}
