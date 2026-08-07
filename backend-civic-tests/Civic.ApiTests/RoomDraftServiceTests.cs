using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// The R7 drafting pipeline, driven deterministically through
/// <see cref="RoomDraftService.DraftBatchAsync"/> with a stub LLM.
///
/// The three <see cref="LlmFailureKind"/> arms are the point of most of this. They were
/// written after an incident in which an unbounded retry against items that could never
/// succeed spent a month's budget on nothing, so each arm's behaviour is asserted rather
/// than assumed to have been copied correctly from BillSynthesisService.
/// </summary>
[Collection("Database")]
public class RoomDraftServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;
    private readonly Guid _briefingId = Guid.NewGuid();
    private readonly string _slug = $"draft-test-{Guid.NewGuid():N}"[..24];

    public RoomDraftServiceTests(DatabaseFixture fx) => _fx = fx;

    private const string SourceProse =
        "The House of Representatives voted to pass a defense appropriations bill totaling "
      + "$1.15 trillion. Tucked inside the bill are provisions that could release additional "
      + "billions of dollars specifically tied to a potential military confrontation.";

    public async Task InitializeAsync()
    {
        await _fx.ResetMutableAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        db.Briefings.Add(new Briefing
        {
            Id = _briefingId,
            Slug = _slug,
            Headline = "House passes a defense appropriations bill",
            Institution = "Congress",
            Branch = "Legislative",
            Status = "Passed House",
            AudienceLevel = "High School",
            KeyConcept = "Appropriations",
            Summary30 = "The House passed a defense bill.",
            Summary3Min = SourceProse,
            Summary10Min = SourceProse,
            WhoActed = "The U.S. House of Representatives",
            WhatChanged = "A defense appropriations bill passed one chamber.",
            WhyItMatters = "Defense spending is one of the largest parts of the budget.",
            Disagreement = "Whether the contingency provisions amount to funding a conflict.",
            StrongestArgumentFor = "Readiness requires planning.",
            StrongestArgumentAgainst = "Pre-authorising war funding lowers the bar to war.",
            ThinkDeeperQuestion = "What is the difference between authorising and appropriating?",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        db.Briefings.RemoveRange(await db.Briefings.Where(b => b.Id == _briefingId).ToListAsync());
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- helpers

    private HttpClient AdminClient()
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.MintAccessToken("admin-1", email: AdminEmail()));
        return client;
    }

    /// <summary>First entry of the Auth:AdminEmails allowlist, read from the host's own
    /// configuration — the dev appsettings carries // comments that JsonDocument rejects.</summary>
    private string AdminEmail()
    {
        var config = _fx.Factory.Services.GetRequiredService<IConfiguration>();
        var first = config.GetSection("Auth:AdminEmails").Get<string[]>()?.FirstOrDefault();
        first.Should().NotBeNullOrWhiteSpace();
        return first!;
    }

    private RoomDraftService Build(ILlmClient llm, int maxAttempts = 3) => new(
        _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
        llm,
        new TestOptionsMonitor<RoomDraftOptions>(new RoomDraftOptions
        {
            Enabled = true,
            DraftBatchSize = 5,
            MaxDraftAttempts = maxAttempts,
        }),
        NullLogger<RoomDraftService>.Instance,
        _fx.Factory.Services.GetRequiredService<StartupReadiness>());

    /// <summary>A candidate story room pointing at the seeded briefing.</summary>
    private async Task<Guid> AddCandidateAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new StoryRoom
        {
            Id = Guid.NewGuid(),
            Slug = $"cand-{_slug}",
            Title = "House passes a defense appropriations bill",
            Dek = "",
            Status = RoomStatus.Candidate,
            StoryType = RoomTopicCategory.Legislative,
            EventTime = DateTime.UtcNow,
            Revision = 1,
            SourceBriefingId = _briefingId,
            GenerationSource = CivicGenerationSource.Model,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private async Task<StoryRoom?> ReloadAsync(Guid id)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        return await db.StoryRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    private static ILlmClient Canned(object result) => new OutageLlmClient(
        new Dictionary<string, string>
        {
            [nameof(RoomDraftResult)] = JsonSerializer.Serialize(result),
        });

    private static object GoodDraft(string passage) => new
    {
        title = "House passes a $1.15 trillion defense bill",
        dek = "The bill has passed one chamber and provides nothing yet.",
        storyType = "Legislative",
        howItWorksIntro = "An appropriations bill must pass both chambers and be signed.",
        whyItMatters = new[]
        {
            new { dimension = "Financial", text = "The figure sits at the first rung.", claimIndex = (int?)0 },
        },
        stakeholders = new[]
        {
            new { group = "Defense contractors", impactSummary = "Nothing changes until enactment.", confidence = 0.6 },
        },
        nextSteps = new[]
        {
            new { description = "The Senate takes up the bill.", verificationCondition = "A Senate roll-call vote is recorded.", expectedTiming = "This session" },
        },
        claims = new[]
        {
            new
            {
                text = "The House passed a defense appropriations bill totaling $1.15 trillion.",
                kind = "Factual",
                status = "Confirmed",
                evidenceSummary = "Stated in the source briefing.",
                whatWouldSettleIt = "The House roll-call record and the engrossed bill text.",
                supportingPassage = passage,
            },
        },
        contestedTermsNoticed = new[] { "spending" },
    };

    // ---------------------------------------------------------------- happy path

    [Fact]
    public async Task ACandidateBecomesADraft_AndTheDraftIsNeverPublished()
    {
        // The whole safety property of the pipeline in one assertion. Drafting has no path
        // to Published; reaching a reader is a separate human action through the gates.
        var id = await AddCandidateAsync();

        var drafted = await Build(Canned(GoodDraft(
            "voted to pass a defense appropriations bill totaling $1.15 trillion")))
            .DraftBatchAsync();

        drafted.Should().Be(1);

        var room = await ReloadAsync(id);
        room!.Status.Should().Be(RoomStatus.Draft);
        room.Status.Should().NotBe(RoomStatus.Published);
        room.Dek.Should().NotBeNullOrWhiteSpace();
        room.DraftedAt.Should().NotBeNull();
        room.DraftPromptVersion.Should().Be(RoomPrompts.Version);
        room.NextSteps.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n.VerificationCondition));
    }

    [Fact]
    public async Task AVerifiedClaimGetsAnEvidenceEdge_ButNeverConfirmedStatus()
    {
        await AddCandidateAsync();

        await Build(Canned(GoodDraft(
            "voted to pass a defense appropriations bill totaling $1.15 trillion")))
            .DraftBatchAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var claim = await db.Claims.AsNoTracking()
            .FirstOrDefaultAsync(c => c.GenerationSource == CivicGenerationSource.Model);

        claim.Should().NotBeNull();
        // The model asked for Confirmed. A briefing is not a primary document, so the cap
        // applies even though the passage checked out.
        claim!.Status.Should().Be(ClaimStatus.StronglySupported);

        var supported = await db.Set<ObjectLink>().AsNoTracking().AnyAsync(l =>
            l.FromType == ObjectType.Claim && l.FromId == claim.Id
            && l.Relation == LinkRelation.SupportedBy && l.ValidTo == null);
        supported.Should().BeTrue();
    }

    [Fact]
    public async Task AParaphrasedPassage_LosesItsEvidenceEdgeAndIsDemoted()
    {
        // Same meaning, different words. This is the output that looks exactly like the good
        // case, so it has to be caught here or not at all.
        await AddCandidateAsync();

        await Build(Canned(GoodDraft(
            "The House approved a $1.15 trillion defense appropriations package")))
            .DraftBatchAsync();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var claim = await db.Claims.AsNoTracking()
            .FirstOrDefaultAsync(c => c.GenerationSource == CivicGenerationSource.Model);

        claim.Should().NotBeNull();
        claim!.Status.Should().Be(ClaimStatus.PlausibleButUnresolved);
        claim.EvidenceSummary.Should().Contain("could not verify");

        var supported = await db.Set<ObjectLink>().AsNoTracking().AnyAsync(l =>
            l.FromType == ObjectType.Claim && l.FromId == claim.Id
            && l.Relation == LinkRelation.SupportedBy && l.ValidTo == null);
        supported.Should().BeFalse("an unverified passage earns no evidence edge");
    }

    // ---------------------------------------------------------------- the three failure arms

    [Fact]
    public async Task CallFailed_RequeuesTheCandidateAndUnCountsTheAttempt()
    {
        // The API is dead. Working through the queue against it burns money and changes
        // nothing, so the batch halts and the item keeps its remaining attempts.
        var id = await AddCandidateAsync();

        var drafted = await Build(new FailingLlmClient()).DraftBatchAsync();

        drafted.Should().Be(0);
        var room = await ReloadAsync(id);
        room!.Status.Should().Be(RoomStatus.Candidate);
        room.DraftAttemptCount.Should().Be(0, "a dead API is not this candidate's fault");
    }

    [Fact]
    public async Task Unavailable_LeavesCandidatesUntouchedForWhenTheLlmReturns()
    {
        // Kill-switch off or no key. Same shape as CallFailed, quieter log.
        var id = await AddCandidateAsync();

        var drafted = await Build(new KeylessLlmClient()).DraftBatchAsync();

        drafted.Should().Be(0);
        var room = await ReloadAsync(id);
        room!.Status.Should().Be(RoomStatus.Candidate);
        room.DraftAttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task BadResponse_KeepsTheAttemptSoAPoisonItemCannotLoopForever()
    {
        // The API is healthy and THIS document is the problem. Un-counting the attempt would
        // pin it at the head of the batch and stall everything behind it — which is exactly
        // how the bill-synthesis retry loop happened.
        var id = await AddCandidateAsync();

        // An all-defaults object: what a refusal that merely quotes the requested shape
        // deserializes into. Persisting it would leave a Draft room with no content.
        var empty = Canned(new { title = "", dek = "", claims = Array.Empty<object>() });

        var drafted = await Build(empty).DraftBatchAsync();

        drafted.Should().Be(0);
        var room = await ReloadAsync(id);
        room!.Status.Should().Be(RoomStatus.Candidate, "it did not become an empty Draft");
        room.DraftAttemptCount.Should().Be(1, "the attempt is kept so retries are bounded");
        room.LastError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AnExhaustedCandidate_StopsBeingRetried()
    {
        var id = await AddCandidateAsync();
        var svc = Build(Canned(new { title = "", dek = "", claims = Array.Empty<object>() }), maxAttempts: 2);

        await svc.DraftBatchAsync();
        await svc.DraftBatchAsync();
        var thirdPass = await svc.DraftBatchAsync();

        thirdPass.Should().Be(0);
        var room = await ReloadAsync(id);
        room!.DraftAttemptCount.Should().Be(2, "the ceiling is what turns a loop into a log line");
    }

    [Fact]
    public async Task ACandidateWithNoBriefingOrBill_FailsRatherThanDraftingFromNothing()
    {
        // The case a news item would land in. There is no body to draft from and no text to
        // verify a passage against, which is why the candidate pass never creates one.
        Guid id;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var room = new StoryRoom
            {
                Id = Guid.NewGuid(),
                Slug = $"sourceless-{_slug}",
                Title = "No source",
                Status = RoomStatus.Candidate,
                EventTime = DateTime.UtcNow,
                Revision = 1,
            };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();
            id = room.Id;
        }

        await Build(Canned(GoodDraft("voted to pass a defense appropriations bill totaling $1.15 trillion")))
            .DraftBatchAsync();

        var reloaded = await ReloadAsync(id);
        reloaded!.Status.Should().Be(RoomStatus.Candidate);
        reloaded.LastError.Should().Contain("no briefing or bill source");
    }

    // ---------------------------------------------------------------- the candidate pass

    [Fact]
    public async Task TheCandidatePass_DoesNotOverwriteASeededRoomsDenominator()
    {
        // The pilot's "we logged 177 articles" was measured against production's corpus. A
        // dev box holds a different library, so recomputing here would replace a true number
        // with one measured against the wrong shelf — and it would read as a silent content
        // change rather than as the environment difference it is.
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = $"seeded-{_slug}",
            Title = "A hand-authored room",
            Status = RoomStatus.Published,
            Revision = 1,
            MatchTerms = new[] { "appropriations", "defense" },
            DevelopmentWindowDays = 23,
            ArticlesConsideredCount = 177,
            GenerationSource = CivicGenerationSource.Seed,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var candidates = scope.ServiceProvider.GetRequiredService<RoomCandidateService>();
        await candidates.ScanRoomAsync(room, new RoomDraftOptions());

        var reloaded = await db.ThemeRooms.AsNoTracking().FirstAsync(r => r.Id == room.Id);
        reloaded.ArticlesConsideredCount.Should().Be(177);
    }

    [Fact]
    public async Task TheCandidatePass_RecomputesTheDenominatorForRoomsItManages()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = $"managed-{_slug}",
            Title = "A pipeline-managed room",
            Status = RoomStatus.Published,
            Revision = 1,
            MatchTerms = new[] { "appropriations", "defense" },
            DevelopmentWindowDays = 23,
            ArticlesConsideredCount = 9999,
            GenerationSource = CivicGenerationSource.Model,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var candidates = scope.ServiceProvider.GetRequiredService<RoomCandidateService>();
        await candidates.ScanRoomAsync(room, new RoomDraftOptions());

        var reloaded = await db.ThemeRooms.AsNoTracking().FirstAsync(r => r.Id == room.Id);
        reloaded.ArticlesConsideredCount.Should().NotBe(9999,
            "the denominator has to reflect what was actually scanned this pass");
    }

    [Fact]
    public async Task TheCandidatePass_CreatesACandidateFromABriefing()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = $"match-{_slug}",
            Title = "Matching room",
            Status = RoomStatus.Published,
            Revision = 1,
            // Two terms, because one is too loose to filter anything useful.
            MatchTerms = new[] { "appropriations", "defense" },
            DevelopmentWindowDays = 30,
            GenerationSource = CivicGenerationSource.Model,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var candidates = scope.ServiceProvider.GetRequiredService<RoomCandidateService>();
        var created = await candidates.ScanRoomAsync(room, new RoomDraftOptions());

        created.Should().BeGreaterThan(0);

        var candidate = await db.StoryRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SourceBriefingId == _briefingId);

        candidate.Should().NotBeNull();
        candidate!.Status.Should().Be(RoomStatus.Candidate, "the pass finds, it does not write");
        candidate.Slug.Should().StartWith(room.Slug + "-");

        // Running twice must not duplicate it — the pass is idempotent by source id.
        var again = await candidates.ScanRoomAsync(room, new RoomDraftOptions());
        again.Should().Be(0);
    }

    // ---------------------------------------------------------------- the optional review surface

    [Fact]
    public async Task ThePipelineReport_IsAdminOnly()
    {
        var res = await _fx.Factory.CreateClient().GetAsync("/api/admin/rooms/pipeline");

        res.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ThePipelineReport_CountsWhatTheDraftPassProduced()
    {
        await AddCandidateAsync();
        await Build(Canned(GoodDraft(
            "voted to pass a defense appropriations bill totaling $1.15 trillion")))
            .DraftBatchAsync();

        var report = await AdminClient()
            .GetFromJsonAsync<RoomPipelineDto>("/api/admin/rooms/pipeline");

        report!.DraftCount.Should().BeGreaterThan(0);
        report.Items.Should().OnlyContain(i => i.Status != "Published");
        report.Items.Should().Contain(i => i.SourceKind == "Briefing" && i.ClaimCount > 0);
    }
}
