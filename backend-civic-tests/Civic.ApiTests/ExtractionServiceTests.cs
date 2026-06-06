using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Services.Coalition;
using Civic.ApiTests.Coalition;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Civic.ApiTests;

/// <summary>
/// Phase 0.3 — extraction function + fidelity harness.
///
/// Three layers of test:
///  1. <see cref="Scorer_IsCorrect_OnSyntheticPredictions"/> — proves the fidelity
///     SCORER math offline (no LLM), so the harness itself is trustworthy.
///  2. <see cref="Extraction_IsCachedByTextHash"/> — proves "cached by text hash":
///     a repeat call with the same text + known set does not re-hit the LLM.
///  3. <see cref="LiveExtraction_MeetsFidelityThreshold"/> — the REAL fidelity gate.
///     Runs the hand-labeled corpus through the live extraction LLM and asserts
///     position accuracy >= threshold and perfect new-sub-question detection.
///     STATICALLY SKIPPED here because this environment has no Anthropic:ApiKey;
///     a manual Claude-in-the-loop pass over the same corpus is recorded in
///     BUILD_LOG.md for human review. Remove the Skip and supply a key to run it.
/// </summary>
[Collection("Database")]
public class ExtractionServiceTests : IAsyncLifetime
{
    private const double FidelityThreshold = 0.80;

    private readonly DatabaseFixture _fx;
    private readonly ITestOutputHelper _out;

    public ExtractionServiceTests(DatabaseFixture fx, ITestOutputHelper @out)
    {
        _fx = fx;
        _out = @out;
    }

    public async Task InitializeAsync() => await _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------
    // 1. Scorer math (offline)
    // ---------------------------------------------------------------
    [Fact]
    public void Scorer_IsCorrect_OnSyntheticPredictions()
    {
        var known = ExtractionFidelityCorpus.Cases.First(c => c.Id == "SD-1").Known;

        // Case with 2 gold positions, one right one wrong, no new-subQ expected.
        var caseA = new FidelityCase("A", "p", known,
            "text",
            new Dictionary<string, string> { ["floor-vs-ceiling"] = "floor", ["enforcement-authority"] = "federal-agency" },
            ExpectsNewSubQuestion: false);
        var resultA = new ExtractionResult
        {
            Positions = new() { ["floor-vs-ceiling"] = "Floor", ["enforcement-authority"] = "state-ag" },
        };

        // Case expecting a new sub-question, which fired; its one gold position is right.
        var caseB = new FidelityCase("B", "p", known,
            "text",
            new Dictionary<string, string> { ["floor-vs-ceiling"] = "floor" },
            ExpectsNewSubQuestion: true);
        var resultB = new ExtractionResult
        {
            Positions = new() { ["floor-vs-ceiling"] = "floor" },
            NewSubQuestions = { new ExtractedNewSubQuestion { Key = "x", Prompt = "?" } },
        };

        var report = ExtractionFidelityScorer.Score(new[] { (caseA, resultA), (caseB, resultB) });

        report.TotalGoldPositions.Should().Be(3);
        report.TotalMatchedPositions.Should().Be(2);     // floor-vs-ceiling x2 right, enforcement wrong
        report.PositionAccuracy.Should().BeApproximately(2.0 / 3.0, 1e-9);
        report.NewSubQCasesTotal.Should().Be(2);
        report.NewSubQCasesCorrect.Should().Be(2);       // A: not expected & not fired; B: expected & fired
        report.NewSubQDetectionPerfect.Should().BeTrue();

        // Detection failure is caught: a case that should fire but didn't.
        var caseC = caseB with { Id = "C" };
        var resultCNoFire = new ExtractionResult { Positions = new() { ["floor-vs-ceiling"] = "floor" } };
        var report2 = ExtractionFidelityScorer.Score(new[] { (caseC, resultCNoFire) });
        report2.NewSubQDetectionPerfect.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // 2. Cache by text hash (offline, stub LLM counts calls)
    // ---------------------------------------------------------------
    [Fact]
    public async Task Extraction_IsCachedByTextHash()
    {
        var known = ExtractionFidelityCorpus.Cases.First(c => c.Id == "SD-1").Known;
        var llm = new StubLlmClient().WithJson<ExtractionResultDto>(
            "{\"positions\":{\"floor-vs-ceiling\":\"floor\"},\"newSubQuestions\":[]}");

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var svc = new ExtractionService(db, llm, NullLogger<ExtractionService>.Instance);

        const string text = "Set a national floor that states may exceed.";

        var r1 = await svc.ExtractAsync(text, known);
        var r2 = await svc.ExtractAsync(text, known); // identical -> cache hit, no second LLM call
        r1.Positions["floor-vs-ceiling"].Should().Be("floor");
        r2.Positions["floor-vs-ceiling"].Should().Be("floor");
        llm.Calls.Count(c => c.Type == nameof(ExtractionResultDto)).Should().Be(1, "second identical call is served from cache");

        // Different text -> cache miss -> a second LLM call.
        await svc.ExtractAsync(text + " The FTC enforces it.", known);
        llm.Calls.Count(c => c.Type == nameof(ExtractionResultDto)).Should().Be(2);

        // A cache row was persisted (keyed by text hash + known signature).
        var hash = ExtractionService.HashText(text);
        var sig = ExtractionService.KnownSignature(known);
        (await db.ExtractionCacheEntries.AnyAsync(e => e.TextHash == hash && e.KnownSignature == sig))
            .Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // 3. THE fidelity gate (live LLM). Skipped without an API key.
    // ---------------------------------------------------------------
    [Fact(Skip = "Requires Anthropic:ApiKey (live extraction LLM). Run the fidelity harness manually with a key set; a Claude-in-the-loop pass over the same corpus is recorded in BUILD_LOG.md. Remove this Skip to enforce the threshold in CI once a key is available.")]
    public async Task LiveExtraction_MeetsFidelityThreshold()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IExtractionService>();

        var runs = new List<(FidelityCase, ExtractionResult)>();
        foreach (var c in ExtractionFidelityCorpus.Cases)
        {
            var result = await svc.ExtractAsync(c.VersionText, c.Known);
            runs.Add((c, result));
        }

        var report = ExtractionFidelityScorer.Score(runs);

        _out.WriteLine($"Position accuracy: {report.PositionAccuracy:P1} ({report.TotalMatchedPositions}/{report.TotalGoldPositions})");
        _out.WriteLine($"New-sub-question detection: {report.NewSubQCasesCorrect}/{report.NewSubQCasesTotal}");
        foreach (var s in report.CaseScores)
            foreach (var m in s.Mismatches)
                _out.WriteLine("  MISS " + m);

        report.PositionAccuracy.Should().BeGreaterThanOrEqualTo(FidelityThreshold);
        report.NewSubQDetectionPerfect.Should().BeTrue();
    }
}
