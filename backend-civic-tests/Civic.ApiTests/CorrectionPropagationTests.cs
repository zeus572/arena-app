using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Civic.API.Controllers.Api;
using Civic.API.Data;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Correction fan-out — PRD 08 Gate 1.
///
/// "Do not launch Theme Rooms until Story Rooms have reliable provenance and corrections."
/// This file is the evidence for that gate: a claim's status moves, and every room,
/// interaction and development that rests on it either updates automatically or lands in
/// the review queue with a named action.
/// </summary>
[Collection("Database")]
public class CorrectionPropagationTests
{
    private readonly DatabaseFixture _fx;

    public CorrectionPropagationTests(DatabaseFixture fx) => _fx = fx;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private HttpClient AdminClient()
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.MintAccessToken("admin-1", email: AdminEmail()));
        return client;
    }

    /// <summary>
    /// The first entry of the Auth:AdminEmails allowlist, read from the host's own
    /// configuration rather than by parsing appsettings — the dev file carries // comments,
    /// which JsonDocument rejects, and the allowlist is environment-specific anyway.
    /// </summary>
    private string AdminEmail()
    {
        var config = _fx.Factory.Services.GetRequiredService<IConfiguration>();
        var first = config.GetSection("Auth:AdminEmails").Get<string[]>()?.FirstOrDefault();

        first.Should().NotBeNullOrWhiteSpace(
            "the admin tests need at least one entry in Auth:AdminEmails");

        return first!;
    }

    /// <summary>
    /// A claim cited by two rooms, one interaction-shaped dependent and one development —
    /// the fan-out shape design 1z draws.
    /// </summary>
    private async Task<(Guid ClaimId, string ClaimSlug, Guid RoomA, Guid RoomB, Guid DevId)>
        SeedFanOutAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var links = scope.ServiceProvider.GetRequiredService<ObjectLinkService>();

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            Slug = "obligation-total",
            NormalizedTextHash = Guid.NewGuid().ToString("N"),
            Text = "No funds from the supplemental have been obligated.",
            Status = ClaimStatus.StronglySupported,
            WhatWouldSettleIt = "A published apportionment record.",
        };
        db.Claims.Add(claim);

        var roomA = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = "room-a",
            Title = "Room A",
            Dek = "First room citing the claim.",
            Status = RoomStatus.Published,
            CurrentStatusSentence = "No money has moved yet.",
        };
        roomA.EssentialFacts.Add(new EssentialFact
        {
            Text = "No funds have been obligated.",
            ClaimId = claim.Id,
            Ordinal = 0,
        });

        var roomB = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = "room-b",
            Title = "Room B",
            Dek = "Second room citing the same claim.",
            Status = RoomStatus.Published,
            CurrentStatusSentence = "Related, and citing the same fact.",
        };

        db.Rooms.AddRange(roomA, roomB);

        var dev = new Development
        {
            Id = Guid.NewGuid(),
            RoomId = roomA.Id,
            OccurredAt = DateTime.UtcNow.AddDays(-1),
            Headline = "Committee reported the supplemental",
            WhyItMatters = "It can now reach the floor.",
            InclusionReason = "An official body acted.",
            EvidenceStatus = ClaimStatus.StronglySupported,
        };
        db.Developments.Add(dev);

        await db.SaveChangesAsync();

        var claimRef = new ObjectRef(ObjectType.Claim, claim.Id);
        await links.LinkAsync(new ObjectRef(ObjectType.Room, roomA.Id),
            LinkRelation.EssentialFact, claimRef);
        await links.LinkAsync(new ObjectRef(ObjectType.Room, roomB.Id),
            LinkRelation.References, claimRef);
        await links.LinkAsync(new ObjectRef(ObjectType.Development, dev.Id),
            LinkRelation.References, claimRef);

        return (claim.Id, claim.Slug, roomA.Id, roomB.Id, dev.Id);
    }

    private async Task<PropagationDto> ChangeStatusAsync(
        string claimSlug,
        string status = "Disputed",
        string kind = "Correction",
        DateTime? sourceCorrectedAt = null)
    {
        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/claims/{claimSlug}/status",
            new
            {
                status,
                changeKind = kind,
                rationale = "A second primary document contradicts the first.",
                sourceCorrectedAt = sourceCorrectedAt ?? DateTime.UtcNow.AddHours(-3),
            });

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<PropagationDto>(Json))!;
    }

    // ------------------------------------------------------------------ the flagship

    [Fact]
    public async Task AStatusChange_WritesHistoryCarryingTheSourceCorrectionTime()
    {
        // The published metric is time-from-SOURCE-correction, which cannot be derived from
        // anything we observe. If this field is ever dropped, the metric silently becomes
        // time-from-our-noticing, which is the flattering version.
        await _fx.ResetMutableAsync();
        var (claimId, slug, _, _, _) = await SeedFanOutAsync();
        var corrected = DateTime.UtcNow.AddHours(-5);

        await ChangeStatusAsync(slug, sourceCorrectedAt: corrected);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var history = await db.ClaimStatusHistories
            .Where(h => h.ClaimId == claimId)
            .OrderByDescending(h => h.ChangedAt)
            .FirstAsync();

        history.FromStatus.Should().Be(ClaimStatus.StronglySupported);
        history.ToStatus.Should().Be(ClaimStatus.Disputed);
        history.SourceCorrectedAt.Should().BeCloseTo(corrected, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task AStatusChange_AddsAChangelogEntryToEveryRoomCitingTheClaim()
    {
        await _fx.ResetMutableAsync();
        var (claimId, slug, roomA, roomB, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        foreach (var roomId in new[] { roomA, roomB })
        {
            var entry = await db.ChangeLogEntries
                .Where(e => e.RoomId == roomId && e.ObjectId == claimId)
                .OrderByDescending(e => e.RevisionNumber)
                .FirstOrDefaultAsync();

            entry.Should().NotBeNull("room {0} cites the claim", roomId);
            entry!.Type.Should().Be(ChangeType.CorrectionIssued);
            entry.IsMeaningful.Should().BeTrue();
            entry.FromValue.Should().Be("StronglySupported");
            entry.ToValue.Should().Be("Disputed");
        }
    }

    [Fact]
    public async Task TheCorrection_ReachesFollowersAsACorrectionNotAnUpdate()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        var reader = _fx.Factory.CreateClient();
        reader.DefaultRequestHeaders.Add("X-User-Id", "reader-1");
        await reader.PostAsJsonAsync("/api/rooms/room-a/seen", new { revision = 1 });

        await ChangeStatusAsync(slug);

        var delta = await reader.GetFromJsonAsync<RoomDeltaDto>(
            "/api/rooms/room-a/delta?sinceRevision=1", Json);

        delta!.Corrections.Should().ContainSingle();
        delta.MeaningfulChanges.Should().NotContain(c => c.Type == "CorrectionIssued");
    }

    [Fact]
    public async Task TheEssentialFactRoom_IsFlaggedForRewrite()
    {
        // Room A presents the claim as an essential fact, so its surrounding prose was
        // written around a status that no longer holds. Room B merely references it.
        await _fx.ResetMutableAsync();
        var (_, slug, roomA, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var flag = await db.ReviewFlags.FirstOrDefaultAsync(f =>
            f.ObjectType == ObjectType.Room && f.ObjectId == roomA && f.ResolvedAt == null);

        flag.Should().NotBeNull();
        flag!.Action.Should().Be(ReviewAction.Rewrite);
        flag.Reason.Should().Be(ReviewReason.DependsOnChangedClaim);
    }

    [Fact]
    public async Task TheDevelopment_SyncsItsStatusAndIsFlaggedForRewrite()
    {
        // The status is the claim's to set, so it syncs automatically. The prose around it
        // is a human's problem.
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, devId) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var dev = await db.Developments.FirstAsync(d => d.Id == devId);
        dev.EvidenceStatus.Should().Be(ClaimStatus.Disputed);

        (await db.ReviewFlags.AnyAsync(f =>
            f.ObjectType == ObjectType.Development
            && f.ObjectId == devId
            && f.Action == ReviewAction.Rewrite
            && f.ResolvedAt == null)).Should().BeTrue();
    }

    [Fact]
    public async Task ThePropagationView_ReportsTheFanOut()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);

        var dto = await AdminClient()
            .GetFromJsonAsync<PropagationDto>($"/api/admin/rooms/propagation/{slug}", Json);

        dto!.CurrentStatus.Should().Be("Disputed");
        dto.Dependents.Should().HaveCountGreaterThanOrEqualTo(3,
            "two rooms and a development reference the claim");
        dto.Flags.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RerunningTheSameCorrection_DoesNotDuplicateFlags()
    {
        // Propagation is re-runnable by design. If it spammed the queue, the queue would
        // stop being read, and then the six-hour rule protects nothing.
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);
        var afterFirst = await CountOpenFlagsAsync();

        await ChangeStatusAsync(slug, status: "Disputed");
        var afterSecond = await CountOpenFlagsAsync();

        afterSecond.Should().Be(afterFirst);
    }

    [Fact]
    public async Task TheFlaggedStatusSentence_DisappearsFromNewSessionsAfterSixHours()
    {
        // The rule, end to end. Backdating the flag is the only way to test a six-hour
        // boundary without a clock abstraction threaded through the whole read path.
        await _fx.ResetMutableAsync();
        var (_, slug, roomA, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);

        var fresh = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/room-a", Json);
        fresh!.CurrentStatusSentence.Should().NotBeEmpty("the grace period has not expired");
        fresh.StatusSentenceUnderReview.Should().BeFalse();

        await BackdateFlagsAsync(roomA, TimeSpan.FromHours(7));

        var stale = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/room-a", Json);

        stale!.CurrentStatusSentence.Should().BeEmpty();
        stale.StatusSentenceUnderReview.Should().BeTrue();
        stale.Title.Should().NotBeEmpty("only the sentence is withheld, not the room");
        stale.EssentialFacts.Should().NotBeEmpty("the facts and their marks are still true");
    }

    [Fact]
    public async Task AReaderAlreadyMidSession_StillSeesIt()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, roomA, _, _) = await SeedFanOutAsync();

        var reader = _fx.Factory.CreateClient();
        reader.DefaultRequestHeaders.Add("X-User-Id", "mid-session-reader");
        await reader.PostAsJsonAsync("/api/rooms/room-a/seen", new { revision = 1 });

        await ChangeStatusAsync(slug);
        await PlaceReaderMidSessionAsync("mid-session-reader", roomA);

        var dto = await reader.GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/room-a", Json);

        dto!.StatusSentenceUnderReview.Should().BeFalse();
        dto.CurrentStatusSentence.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ResolvingTheFlag_BringsTheSentenceBack()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, roomA, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug);
        await BackdateFlagsAsync(roomA, TimeSpan.FromHours(7));

        Guid flagId;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            flagId = await db.ReviewFlags
                .Where(f => f.ObjectType == ObjectType.Room && f.ObjectId == roomA)
                .Select(f => f.Id).FirstAsync();
        }

        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/flags/{flagId}/resolve",
            new { resolution = "Rewritten", note = "Status sentence updated." });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = await _fx.Factory.CreateClient()
            .GetFromJsonAsync<ThemeRoomDetailDto>("/api/rooms/room-a", Json);

        dto!.StatusSentenceUnderReview.Should().BeFalse();
    }

    // ------------------------------------------------------------------ guards

    [Fact]
    public async Task ACorrection_RequiresTheSourceCorrectionTime()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/claims/{slug}/status",
            new { status = "Disputed", changeKind = "Correction", rationale = "Because." });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("source_corrected_at_required");
    }

    [Fact]
    public async Task AStatusChange_RequiresARationale()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/claims/{slug}/status",
            new { status = "Disputed", changeKind = "NewEvidence", rationale = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OverridingAFlag_RequiresANote()
    {
        // Otherwise "Overridden" becomes the button people click to clear the queue
        // without reading it.
        await _fx.ResetMutableAsync();
        var (_, slug, roomA, _, _) = await SeedFanOutAsync();
        await ChangeStatusAsync(slug);

        Guid flagId;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            flagId = await db.ReviewFlags.Where(f => f.ObjectId == roomA)
                .Select(f => f.Id).FirstAsync();
        }

        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/flags/{flagId}/resolve", new { resolution = "Overridden" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("override_note_required");
    }

    [Fact]
    public async Task TheAdminApi_IsClosedToNonAdmins()
    {
        await _fx.ResetMutableAsync();

        var user = _fx.Factory.CreateClient();
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.MintAccessToken("ordinary-user", email: "nobody@example.com"));

        foreach (var path in new[]
        {
            "/api/admin/rooms/flags",
            "/api/admin/rooms/metrics",
            "/api/admin/rooms/integrity",
        })
        {
            var status = (await user.GetAsync(path)).StatusCode;
            status.Should().Match(s =>
                s == HttpStatusCode.Forbidden || s == HttpStatusCode.Unauthorized,
                "{0} must be closed to non-admins but returned {1}", path, status);
        }
    }

    [Fact]
    public async Task WithdrawingASource_FlagsEveryClaimRestingOnIt()
    {
        await _fx.ResetMutableAsync();
        var (claimId, _, _, _, _) = await SeedFanOutAsync();

        Guid sourceId;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var links = scope.ServiceProvider.GetRequiredService<ObjectLinkService>();

            var source = new SourceRef
            {
                Id = Guid.NewGuid(),
                Url = "https://example.gov/report",
                UrlHash = Guid.NewGuid().ToString("N"),
                Title = "Agency report",
                SourceType = SourceType.GovernmentData,
                IsPrimary = true,
            };
            db.SourceRefs.Add(source);
            await db.SaveChangesAsync();
            sourceId = source.Id;

            await links.LinkAsync(new ObjectRef(ObjectType.Claim, claimId),
                LinkRelation.SupportedBy, new ObjectRef(ObjectType.SourceRef, sourceId));
        }

        var res = await AdminClient().PostAsJsonAsync(
            $"/api/admin/rooms/sources/{sourceId}/withdraw", new { availability = "Retracted" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var check = _fx.Factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<CivicDbContext>();

        (await db2.ReviewFlags.AnyAsync(f =>
            f.ObjectType == ObjectType.Claim
            && f.ObjectId == claimId
            && f.Reason == ReviewReason.SourceWithdrawn
            && f.ResolvedAt == null)).Should().BeTrue();
    }

    [Fact]
    public async Task Metrics_ReportTimeFromTheSourceCorrectionNotFromOurNoticing()
    {
        await _fx.ResetMutableAsync();
        var (_, slug, _, _, _) = await SeedFanOutAsync();

        await ChangeStatusAsync(slug, sourceCorrectedAt: DateTime.UtcNow.AddHours(-4));

        var metrics = await AdminClient()
            .GetFromJsonAsync<RoomMetricsDto>("/api/admin/rooms/metrics", Json);

        metrics!.CorrectionsIssued.Should().BeGreaterThan(0);
        metrics.MedianHoursFromSourceCorrection.Should().BeApproximately(4, 0.5);
    }

    [Fact]
    public async Task Integrity_ReportsNoDanglingEdgesForAWellFormedGraph()
    {
        await _fx.ResetMutableAsync();
        await SeedFanOutAsync();

        var dangling = await AdminClient()
            .GetFromJsonAsync<List<DanglingEdgeDto>>("/api/admin/rooms/integrity", Json);

        dangling.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> CountOpenFlagsAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        return await db.ReviewFlags.CountAsync(f => f.ResolvedAt == null);
    }

    /// <summary>Age a room's open flags so the six-hour boundary can be crossed in a test.</summary>
    private async Task BackdateFlagsAsync(Guid roomId, TimeSpan age)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var flags = await db.ReviewFlags
            .Where(f => f.ObjectId == roomId && f.ResolvedAt == null)
            .ToListAsync();

        foreach (var f in flags) f.CreatedAt = DateTime.UtcNow - age;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Put a reader mid-session at the moment the grace period expires: the flag is just
    /// past six hours old, and their last visit is inside the session window but before
    /// the content became hideable.
    /// </summary>
    private async Task PlaceReaderMidSessionAsync(string userId, Guid roomId)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var now = DateTime.UtcNow;
        var flags = await db.ReviewFlags
            .Where(f => f.ObjectId == roomId && f.ResolvedAt == null).ToListAsync();

        // Became hideable five minutes ago.
        foreach (var f in flags)
        {
            f.CreatedAt = now - RoomVisibility.UnreviewedGrace - TimeSpan.FromMinutes(5);
        }

        // Reading for the last ten minutes � so their session predates the hide.
        var state = await db.UserRoomStates
            .FirstAsync(s => s.UserId == userId && s.RoomId == roomId);
        state.LastVisitedAt = now - TimeSpan.FromMinutes(10);

        await db.SaveChangesAsync();
    }
}
