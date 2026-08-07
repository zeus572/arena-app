using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The Topic Rooms read API (docs/Rooms Expansion, PRD 01).
///
/// Rooms are inserted directly rather than seeded, so each test owns its own content —
/// the pilot seed is off in the test host and Respawn truncates every rooms table between
/// cases (a deliberate departure from the read-only-catalog precedent: rooms are mutable
/// editorial content and the propagation tests need a clean graph).
/// </summary>
[Collection("Database")]
public class RoomsApiTests
{
    private readonly DatabaseFixture _fx;

    public RoomsApiTests(DatabaseFixture fx) => _fx = fx;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private HttpClient AnonymousClient() => _fx.Factory.CreateClient();

    private HttpClient ClientFor(string userId)
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    /// <summary>An authenticated, email-verified client — what the write endpoints require.</summary>
    private HttpClient VerifiedClient(string subject)
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.MintAccessToken(subject));
        return client;
    }

    private async Task<ThemeRoom> SeedThemeAsync(
        string slug,
        RoomStatus status = RoomStatus.Published,
        string? locality = null,
        Guid? essentialClaimId = null)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = "Federal appropriations",
            Dek = "Where the money is asked for, and where it actually goes.",
            Status = status,
            Locality = locality,
            Revision = 1,
            CurrentStatusSentence = "Four of five funding stages remain empty.",
            TopUnresolvedQuestion = "Will the supplemental receive a floor vote?",
            WatchNext = "The Senate cloture vote.",
            ArticlesConsideredCount = 260,
            DevelopmentWindowDays = 34,
            InclusionRules = new[] { "An official body acted." },
            ExclusionRules = new[] { "New commentary about an old event." },
        };

        if (essentialClaimId is { } claimId)
        {
            room.EssentialFacts.Add(new EssentialFact
            {
                Text = "No funds have been obligated.",
                ClaimId = claimId,
                Ordinal = 0,
            });
        }

        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    private async Task<Claim> SeedClaimAsync(string slug, ClaimStatus status)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            NormalizedTextHash = Guid.NewGuid().ToString("N"),
            Text = "No funds have been obligated.",
            Status = status,
            WhatWouldSettleIt = "A published apportionment record.",
        };

        db.Claims.Add(claim);
        await db.SaveChangesAsync();
        return claim;
    }

    /// <summary>Commit a revision through the real service, the way every writer must.</summary>
    private async Task CommitAsync(Guid roomId, params PendingChange[] changes)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<RoomRevisionService>();
        await svc.CommitAsync(roomId, "editor@example.com", changes);
    }

    // ------------------------------------------------------------------ reads

    [Fact]
    public async Task Room_IsReadableWithoutAnAccount()
    {
        // The whole point of a room is that you can land on it from a search result and
        // understand it without signing up.
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("appropriations");

        var res = await AnonymousClient().GetAsync("/api/rooms/appropriations");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<ThemeRoomDetailDto>(Json);
        dto!.CurrentStatusSentence.Should().Be("Four of five funding stages remain empty.");
        dto.Kind.Should().Be("Theme");
    }

    [Fact]
    public async Task UnpublishedRoom_IsNotServedPublicly()
    {
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("draft-room", RoomStatus.Draft);

        var res = await AnonymousClient().GetAsync("/api/rooms/draft-room");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EssentialFactStatus_ComesFromTheClaimNotTheRoom()
    {
        // The rule the entire correction story rests on: room copy must never cache a
        // claim's status. Changing the CLAIM alone must change the front door.
        await _fx.ResetMutableAsync();
        var claim = await SeedClaimAsync("no-funds-obligated", ClaimStatus.StronglySupported);
        await SeedThemeAsync("appropriations", essentialClaimId: claim.Id);

        var before = await AnonymousClient()
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        before!.EssentialFacts[0].ClaimStatus.Should().Be("StronglySupported");

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var tracked = await db.Claims.FirstAsync(c => c.Id == claim.Id);
            tracked.Status = ClaimStatus.Disputed;
            await db.SaveChangesAsync();
        }

        var after = await AnonymousClient()
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        after!.EssentialFacts[0].ClaimStatus.Should().Be("Disputed",
            "the front door renders the mark from the claim, so a correction reaches it "
          + "without anyone editing the room");
    }

    [Fact]
    public async Task LocalityScopedRoom_IsHiddenFromOtherStates()
    {
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("wa-only", locality: "WA");

        var res = await AnonymousClient().GetAsync("/api/rooms/wa-only");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "an anonymous reader has no locality, so state-scoped rooms are national-invisible");
    }

    // ------------------------------------------------------------------ delta

    [Fact]
    public async Task Delta_SeparatesMeaningfulChangesFromWithheldEdits()
    {
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");

        await CommitAsync(room.Id,
            new PendingChange(ChangeType.OfficialAction, "Senate voted on cloture",
                WhyItMatters: "The supplemental can now reach the floor."));
        await CommitAsync(room.Id,
            new PendingChange(ChangeType.TypoFix, "Fixed a typo"),
            new PendingChange(ChangeType.SourceAdded, "Added a second source"),
            new PendingChange(ChangeType.CopyEdit, "Tightened the dek"));

        var delta = await AnonymousClient()
            .GetFromJsonAsync<RoomDeltaDto>("/api/rooms/appropriations/delta?sinceRevision=1", Json);

        delta!.MeaningfulChanges.Should().HaveCount(1);
        delta.MeaningfulChanges[0].Headline.Should().Be("Senate voted on cloture");
        delta.WithheldCount.Should().Be(3, "three edits nobody should be interrupted for");
        delta.WithheldByType.Should().HaveCount(3);
        delta.ToRevision.Should().Be(3);
    }

    [Fact]
    public async Task Delta_KeepsCorrectionsInTheirOwnArray()
    {
        // Corrections are never folded into "updated". Enforcing the split at the API
        // boundary means the frontend structurally cannot merge them by accident.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");

        await CommitAsync(room.Id,
            new PendingChange(ChangeType.OfficialAction, "Committee reported the bill"),
            new PendingChange(ChangeType.CorrectionIssued, "We misstated the obligation total",
                CorrectionKind: CorrectionKind.Factual));

        var delta = await AnonymousClient()
            .GetFromJsonAsync<RoomDeltaDto>("/api/rooms/appropriations/delta?sinceRevision=1", Json);

        delta!.Corrections.Should().HaveCount(1);
        delta.Corrections[0].CorrectionKind.Should().Be("Factual");
        delta.MeaningfulChanges.Should().NotContain(c => c.Type == "CorrectionIssued");
    }

    [Fact]
    public async Task Delta_IsEmptyWhenTheReaderIsAlreadyCurrent()
    {
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");
        await CommitAsync(room.Id, new PendingChange(ChangeType.OfficialAction, "A vote"));

        var delta = await AnonymousClient()
            .GetFromJsonAsync<RoomDeltaDto>("/api/rooms/appropriations/delta?sinceRevision=99", Json);

        delta!.HasChanges.Should().BeFalse();
        delta.WithheldCount.Should().Be(0);
    }

    [Fact]
    public async Task ChangeLog_ServesTheWithheldEditsToo()
    {
        // "11 edits we did not bother you with" is only honest if the reader can go look.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");
        await CommitAsync(room.Id, new PendingChange(ChangeType.TypoFix, "Fixed a typo"));

        var entries = await AnonymousClient()
            .GetFromJsonAsync<List<ChangeLogEntryDto>>("/api/rooms/appropriations/changelog", Json);

        entries.Should().ContainSingle(e => e.Type == "TypoFix" && !e.IsMeaningful);
    }

    // ------------------------------------------------------------------ viewer state

    [Fact]
    public async Task MarkSeen_WorksAnonymouslyAndNeverMovesBackwards()
    {
        // PRD 01 §TR-5 wants "since your last visit" to work without an account. And
        // opening an old share link must not un-see a room.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");
        await CommitAsync(room.Id, new PendingChange(ChangeType.OfficialAction, "A vote"));
        await CommitAsync(room.Id, new PendingChange(ChangeType.OfficialAction, "Another vote"));

        var client = ClientFor("reader-1");

        (await client.PostAsJsonAsync("/api/rooms/appropriations/seen", new { revision = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync("/api/rooms/appropriations/seen", new { revision = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await client.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        dto!.Viewer.LastSeenRevision.Should().Be(3);
    }

    [Fact]
    public async Task FirstVisit_GetsNoDelta()
    {
        // Telling someone who has never been here that nothing has changed since their
        // last visit is noise.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");
        await CommitAsync(room.Id, new PendingChange(ChangeType.OfficialAction, "A vote"));

        var dto = await ClientFor("brand-new-reader")
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);

        dto!.Viewer.Delta.Should().BeNull();
        dto.Viewer.LastSeenRevision.Should().Be(0);
    }

    [Fact]
    public async Task ReturningVisit_CarriesTheDeltaInline()
    {
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");

        var client = ClientFor("returning-reader");
        await client.PostAsJsonAsync("/api/rooms/appropriations/seen", new { revision = 1 });

        await CommitAsync(room.Id,
            new PendingChange(ChangeType.PredictionResolved, "The cloture prediction resolved"));

        var dto = await client.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);

        dto!.Viewer.Delta.Should().NotBeNull();
        dto.Viewer.Delta!.MeaningfulChanges.Should().ContainSingle();
    }

    // ------------------------------------------------------------------ writes

    [Fact]
    public async Task Follow_RequiresAnAccount()
    {
        // A follow is a notification commitment, so it is account-bound like every other
        // persistent write in Civic.
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("appropriations");

        var res = await AnonymousClient().PostAsync("/api/rooms/appropriations/follow", null);

        res.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Follow_ThenUnfollow_RoundTrips()
    {
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("appropriations");
        var client = VerifiedClient("follower-1");

        (await client.PostAsync("/api/rooms/appropriations/follow", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var followed = await client.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        followed!.Viewer.Following.Should().BeTrue();

        (await client.DeleteAsync("/api/rooms/appropriations/follow"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unfollowed = await client.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        unfollowed!.Viewer.Following.Should().BeFalse();
    }

    [Fact]
    public async Task SectionProgress_OnlyEverMovesForward()
    {
        // "Nothing is required. The bars just remember where you have been."
        await _fx.ResetMutableAsync();
        await SeedThemeAsync("appropriations");
        var client = VerifiedClient("path-walker");

        await client.PostAsJsonAsync("/api/rooms/appropriations/sections/latest/progress",
            new { itemsSeen = 5, itemsTotal = 8 });
        await client.PostAsJsonAsync("/api/rooms/appropriations/sections/latest/progress",
            new { itemsSeen = 2, itemsTotal = 8 });

        var dto = await client.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/appropriations", Json);
        var section = dto!.Viewer.SectionProgress.Single(p => p.SectionKey == "latest");
        section.ItemsSeen.Should().Be(5);
        section.ItemsTotal.Should().Be(8);
    }

    // ------------------------------------------------------------------ revisions

    [Fact]
    public async Task Revisions_MarkMeaningfulOnesAndCarryASnapshotOnlyForThose()
    {
        // The scrubber draws tall marks for meaningful revisions and short ones for edits;
        // snapshots are written only for the tall ones, which is what keeps diff mode a
        // pure frontend decision later rather than a migration.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<RoomRevisionService>();
            await svc.CommitAsync(room.Id, "editor",
                new[] { new PendingChange(ChangeType.OfficialAction, "A vote") },
                snapshot: new { title = "Federal appropriations" });
            await svc.CommitAsync(room.Id, "editor",
                new[] { new PendingChange(ChangeType.TypoFix, "Typo") },
                snapshot: new { title = "Federal appropriations" });
        }

        var marks = await AnonymousClient()
            .GetFromJsonAsync<List<RoomRevisionMarkDto>>("/api/rooms/appropriations/revisions", Json);

        marks.Should().HaveCount(2);
        marks!.Single(m => m.Revision == 2).IsMeaningful.Should().BeTrue();
        marks.Single(m => m.Revision == 2).HasSnapshot.Should().BeTrue();
        marks.Single(m => m.Revision == 3).IsMeaningful.Should().BeFalse();
        marks.Single(m => m.Revision == 3).HasSnapshot.Should().BeFalse(
            "a typo fix is not worth a few KB of snapshot");
    }

    [Fact]
    public async Task Commit_RefusesARevisionWithNoChanges()
    {
        // Bumping the counter without saying why would make the delta ribbon start lying.
        await _fx.ResetMutableAsync();
        var room = await SeedThemeAsync("appropriations");

        using var scope = _fx.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<RoomRevisionService>();

        var act = () => svc.CommitAsync(room.Id, "editor", Array.Empty<PendingChange>());

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

/// <summary>Mirrors the controller's scrubber DTO for deserialization.</summary>
public class RoomRevisionMarkDto
{
    public int Revision { get; set; }
    public bool IsMeaningful { get; set; }
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool HasSnapshot { get; set; }
}
