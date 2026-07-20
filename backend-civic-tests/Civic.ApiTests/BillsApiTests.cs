using System.Net;
using System.Net.Http.Json;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services;
using Civic.API.Services.Bills;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class BillsApiTests
{
    private readonly DatabaseFixture _fx;

    public BillsApiTests(DatabaseFixture fx) => _fx = fx;

    private BillIngestionService BuildIngestion(InMemoryBillSource source, BillOptions? opts = null)
    {
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new BillIngestionService(
            scopes,
            source,
            new TestOptionsMonitor<BillOptions>(opts ?? new BillOptions { IngestIntervalHours = 24 }),
            NullLogger<BillIngestionService>.Instance,
            _fx.Factory.Services.GetRequiredService<StartupReadiness>());
    }

    private BillSynthesisService BuildSynthesis(ILlmClient llm, BillOptions? opts = null)
    {
        var scopes = _fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new BillSynthesisService(
            scopes,
            llm,
            _fx.Factory.Services.GetRequiredService<ICivicCatalog>(),
            new TestOptionsMonitor<BillOptions>(opts ?? new BillOptions()),
            NullLogger<BillSynthesisService>.Instance,
            _fx.Factory.Services.GetRequiredService<StartupReadiness>());
    }

    private static Bill NewBill(string ext, string title) => new()
    {
        Id = Guid.NewGuid(),
        ExternalId = ext,
        Congress = 118,
        BillType = "HR",
        Number = 42,
        Title = title,
        Summary = "A test bill that does a specific thing with a real tradeoff.",
        Sponsor = "Rep. Test Person",
        Party = "D",
        Status = BillStatus.Introduced,
        IntroducedDate = DateTime.UtcNow.AddDays(-5),
        SynthesisStatus = BillSynthesisStatus.Ingested,
        GenerationSource = "congress",
        IngestedAt = DateTime.UtcNow,
    };

    // ---- Ingestion ----

    [Fact]
    public async Task IngestOnce_PersistsAndIsIdempotent()
    {
        await _fx.ResetMutableAsync();
        var source = new InMemoryBillSource { Bills = { NewBill("hr-42-118", "Test Act") } };
        var svc = BuildIngestion(source);

        var first = await svc.IngestOnceAsync();
        var second = await svc.IngestOnceAsync();

        first.Should().Be(1);
        second.Should().Be(0, "the same ExternalId must not insert twice");

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.Bills.CountAsync(b => b.ExternalId == "hr-42-118")).Should().Be(1);
    }

    [Fact]
    public async Task IngestOnce_Disabled_DoesNothing()
    {
        await _fx.ResetMutableAsync();
        var source = new InMemoryBillSource { Bills = { NewBill("hr-99-118", "Skipped Act") } };
        var svc = BuildIngestion(source, new BillOptions { Enabled = false });

        (await svc.IngestOnceAsync()).Should().Be(0);
    }

    // ---- Synthesis ----

    [Fact]
    public async Task Synthesize_WritesPositions_DropsUnknownAxes_ClampsScores()
    {
        await _fx.ResetMutableAsync();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            db.Bills.Add(NewBill("hr-7-118", "Synthesis Target Act"));
            await db.SaveChangesAsync();
        }

        var llm = new StubLlmClient().WithJson<BillSynthesisResult>("""
        {
          "summary": "Neutral summary of what the bill does.",
          "positions": [
            { "axisKey": "govt-role", "score": 0.6, "confidence": 0.9, "rationale": "Expands a federal program.", "evidence": "Sec. 2" },
            { "axisKey": "religion", "score": 1.8, "confidence": 0.4, "rationale": "Over-range score should clamp.", "evidence": "" },
            { "axisKey": "totally-made-up", "score": 0.5, "confidence": 0.5, "rationale": "Not a real axis.", "evidence": "" }
          ]
        }
        """);

        var svc = BuildSynthesis(llm);
        var done = await svc.SynthesizeBatchAsync();
        done.Should().Be(1);

        using var verify = _fx.Factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<CivicDbContext>();
        var bill = await vdb.Bills.Include(b => b.AxisPositions).SingleAsync(b => b.ExternalId == "hr-7-118");

        bill.SynthesisStatus.Should().Be(BillSynthesisStatus.Synthesized);
        bill.SynthesisSummary.Should().NotBeNullOrWhiteSpace();
        bill.AxisPositions.Should().Contain(p => p.AxisKey == "govt-role");
        bill.AxisPositions.Should().NotContain(p => p.AxisKey == "totally-made-up", "unknown axes are dropped");
        bill.AxisPositions.Single(p => p.AxisKey == "religion").Score.Should().Be(1.0, "scores clamp to [-1,1]");
    }

    [Fact]
    public async Task Synthesize_UnparseableBill_MarksThatBillFailed_AndContinuesBatch()
    {
        // Regression: a bill whose LLM response won't parse (BadResponse) must not poison the
        // whole batch. Before the fix it was tagged CallFailed → requeued + un-counted + break,
        // pinning the bad bill at the head of the queue and stalling every bill behind it.
        await _fx.ResetMutableAsync();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            // "poison" sorts to the head (newest action), so a halting bug would starve the rest.
            db.Bills.Add(WithAction(NewBill("hr-1-118", "Poison Bill"), DateTime.UtcNow));
            db.Bills.Add(WithAction(NewBill("hr-2-118", "Good Bill A"), DateTime.UtcNow.AddDays(-1)));
            db.Bills.Add(WithAction(NewBill("hr-3-118", "Good Bill B"), DateTime.UtcNow.AddDays(-2)));
            await db.SaveChangesAsync();
        }

        var goodJson = """
        { "summary": "Neutral summary.", "positions": [ { "axisKey": "govt-role", "score": 0.2, "confidence": 0.8, "rationale": "r", "evidence": "" } ] }
        """;
        var llm = new PoisonBillLlmClient(poisonTitle: "Poison Bill", goodJson);

        var svc = BuildSynthesis(llm, new BillOptions { SynthesisBatchSize = 10 });
        var done = await svc.SynthesizeBatchAsync();

        done.Should().Be(2, "the two good bills must still synthesize despite the poison bill");

        using var verify = _fx.Factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<CivicDbContext>();
        var byExt = await vdb.Bills.ToDictionaryAsync(b => b.ExternalId!, b => b);

        byExt["hr-1-118"].SynthesisStatus.Should().Be(BillSynthesisStatus.Failed, "the poison bill is failed, not requeued");
        byExt["hr-1-118"].AttemptCount.Should().Be(1, "its attempt must NOT be un-counted (that made it immortal)");
        byExt["hr-2-118"].SynthesisStatus.Should().Be(BillSynthesisStatus.Synthesized);
        byExt["hr-3-118"].SynthesisStatus.Should().Be(BillSynthesisStatus.Synthesized);
    }

    [Fact]
    public async Task Synthesize_DegenerateResult_FailsThatBill_RatherThanEmptySynthesized()
    {
        // The client salvages JSON out of prose, so a refusal that merely QUOTES the shape
        // ("I can't judge this, but the format is {...}") deserializes into an all-defaults
        // result. Persisting that would mark the bill Synthesized with an empty compass —
        // silent dead data that never retries. The empty-result guard must reject it.
        await _fx.ResetMutableAsync();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            db.Bills.Add(WithAction(NewBill("hr-4-118", "Empty Result Bill"), DateTime.UtcNow));
            db.Bills.Add(WithAction(NewBill("hr-5-118", "Good Bill C"), DateTime.UtcNow.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        var llm = new PerTitleJsonLlmClient(
            matchTitle: "Empty Result Bill",
            matchJson: """{ "summary": "", "positions": [] }""",
            otherJson: """
            { "summary": "Neutral summary.", "positions": [ { "axisKey": "govt-role", "score": 0.2, "confidence": 0.8, "rationale": "r", "evidence": "" } ] }
            """);

        var svc = BuildSynthesis(llm, new BillOptions { SynthesisBatchSize = 10 });
        var done = await svc.SynthesizeBatchAsync();

        done.Should().Be(1, "only the bill with real content counts as synthesized");

        using var verify = _fx.Factory.Services.CreateScope();
        var vdb = verify.ServiceProvider.GetRequiredService<CivicDbContext>();
        var byExt = await vdb.Bills.Include(b => b.AxisPositions).ToDictionaryAsync(b => b.ExternalId!, b => b);

        var empty = byExt["hr-4-118"];
        empty.SynthesisStatus.Should().Be(BillSynthesisStatus.Failed,
            "an empty parse is a failure, not a synthesis — otherwise it's dead data that never retries");
        empty.AxisPositions.Should().BeEmpty();
        empty.SynthesisSummary.Should().BeNullOrWhiteSpace("no partial write may survive the guard");
        byExt["hr-5-118"].SynthesisStatus.Should().Be(BillSynthesisStatus.Synthesized, "the batch continues");
    }

    private static Bill WithAction(Bill b, DateTime latestAction)
    {
        b.LatestActionDate = latestAction;
        return b;
    }

    /// <summary>Returns <paramref name="matchJson"/> for the bill whose user prompt contains
    /// <paramref name="matchTitle"/>, else <paramref name="otherJson"/>. Models the client
    /// SUCCEEDING at parsing (no throw) but yielding degenerate content.</summary>
    private sealed class PerTitleJsonLlmClient(string matchTitle, string matchJson, string otherJson) : ILlmClient
    {
        public Task<T> GenerateStructuredAsync<T>(
            string systemPrompt, string userPrompt, LlmModelTier tier = LlmModelTier.Sonnet,
            int? maxTokens = null, CancellationToken ct = default)
        {
            var json = userPrompt.Contains(matchTitle, StringComparison.Ordinal) ? matchJson : otherJson;
            var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(
                json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return Task.FromResult(parsed);
        }
    }

    /// <summary>Throws <see cref="LlmFailureKind.BadResponse"/> for the one bill whose user
    /// prompt contains <paramref name="poisonTitle"/>; returns canned JSON for everything else.</summary>
    private sealed class PoisonBillLlmClient(string poisonTitle, string goodJson) : ILlmClient
    {
        public Task<T> GenerateStructuredAsync<T>(
            string systemPrompt, string userPrompt, LlmModelTier tier = LlmModelTier.Sonnet,
            int? maxTokens = null, CancellationToken ct = default)
        {
            if (userPrompt.Contains(poisonTitle, StringComparison.Ordinal))
            {
                throw new LlmException(
                    "Claude returned non-JSON after retry.", kind: LlmFailureKind.BadResponse);
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(
                goodJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            return Task.FromResult(parsed);
        }
    }

    // ---- Controller ----

    [Fact]
    public async Task List_ReturnsOnlySynthesizedBills()
    {
        await _fx.ResetMutableAsync();
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var synth = NewBill("hr-100-118", "Visible Act");
            synth.SynthesisStatus = BillSynthesisStatus.Synthesized;
            synth.SynthesizedAt = DateTime.UtcNow;
            db.Bills.Add(synth);
            db.Bills.Add(NewBill("hr-101-118", "Pending Act")); // still Ingested
            await db.SaveChangesAsync();
        }

        var client = _fx.Factory.CreateClient();
        var list = await client.GetFromJsonAsync<List<BillSummaryDto>>("/api/bills");

        list.Should().NotBeNull();
        list!.Should().Contain(b => b.ExternalId == "hr-100-118");
        list.Should().NotContain(b => b.ExternalId == "hr-101-118");
        list.Single(b => b.ExternalId == "hr-100-118").Identifier.Should().Contain("118th Congress");
    }

    [Fact]
    public async Task Detail_WithUserProfile_ReturnsAlignment()
    {
        await _fx.ResetMutableAsync();
        var userId = "test-user-" + Guid.NewGuid().ToString("N");
        Guid billId;

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var bill = NewBill("hr-200-118", "Alignment Act");
            bill.SynthesisStatus = BillSynthesisStatus.Synthesized;
            bill.AxisPositions.Add(new BillAxisPosition
            {
                Id = Guid.NewGuid(), BillId = bill.Id, AxisKey = "govt-role",
                Score = 0.8, Confidence = 0.9, Rationale = "Expands government role.",
            });
            db.Bills.Add(bill);
            billId = bill.Id;

            var profile = new UserProfile
            {
                Id = Guid.NewGuid(), UserId = userId, ProfileVersion = 2,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            profile.AxisScores.Add(new ProfileAxisScore
            {
                Id = Guid.NewGuid(), UserProfileId = profile.Id, AxisKey = "govt-role",
                Score = 0.7, Confidence = 1, Intensity = 1,
            });
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        var client = _fx.Factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}");
        req.Headers.Add("X-User-Id", userId);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await resp.Content.ReadFromJsonAsync<BillDetailDto>();
        detail.Should().NotBeNull();
        detail!.HasUserCompass.Should().BeTrue();
        detail.OverallAlignmentPercent.Should().NotBeNull();
        var axis = detail.Axes.Single(a => a.AxisKey == "govt-role");
        axis.UserScore.Should().BeApproximately(0.7, 1e-6);
        axis.Alignment.Should().Be("aligned", "user and bill both lean high on govt-role");
    }

    [Fact]
    public async Task Detail_Anonymous_HasNoCompass()
    {
        await _fx.ResetMutableAsync();
        Guid billId;
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var bill = NewBill("hr-300-118", "Anon Act");
            bill.SynthesisStatus = BillSynthesisStatus.Synthesized;
            bill.AxisPositions.Add(new BillAxisPosition
            {
                Id = Guid.NewGuid(), BillId = bill.Id, AxisKey = "religion",
                Score = -0.5, Confidence = 0.7, Rationale = "Secular framing.",
            });
            db.Bills.Add(bill);
            billId = bill.Id;
            await db.SaveChangesAsync();
        }

        var client = _fx.Factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}");
        req.Headers.Add("X-User-Id", "fresh-anon-" + Guid.NewGuid().ToString("N"));
        var detail = await (await client.SendAsync(req)).Content.ReadFromJsonAsync<BillDetailDto>();

        detail.Should().NotBeNull();
        detail!.HasUserCompass.Should().BeFalse();
        detail.OverallAlignmentPercent.Should().BeNull();
        detail.Axes.Single().UserScore.Should().BeNull();
        detail.Axes.Single().Alignment.Should().BeNull();
    }
}
