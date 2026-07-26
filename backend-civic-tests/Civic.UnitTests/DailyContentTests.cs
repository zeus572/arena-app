using System.Text.Json.Nodes;
using Civic.API.Models.Daily;
using Civic.API.Services;
using Civic.API.Services.Daily;
using Civic.API.Services.Daily.Generators;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// The content guards for the daily games: answer-key redaction, the Whose Value leak
/// filter, the Fork neutrality validator, and the seed banks' balance rules.
/// </summary>
public class DailyContentTests
{
    // ------------------------------------------------------------- Redaction

    [Fact]
    public void Redaction_CrowdCall_StripsTrueRateAndSampleSize()
    {
        var payload = new CrowdCallPayload(new List<CrowdCallRound>
        {
            new("prompt", "answer", "why", CrowdSource.CivicUsers, "players", null, null, 412, 0.68),
        });

        var redacted = DailyRedaction.Redact(DailyGameKind.CrowdCall, DailyJson.Serialize(payload));

        var round = redacted["rounds"]!.AsArray()[0]!.AsObject();
        round.ContainsKey("trueRate").Should().BeFalse();
        round.ContainsKey("sampleSize").Should().BeFalse();
        // Everything the player legitimately needs survives.
        round["prompt"]!.GetValue<string>().Should().Be("prompt");
        round["attribution"]!.GetValue<string>().Should().Be("players");
    }

    [Fact]
    public void Redaction_PricedIn_StripsTrueValueAndAnchor()
    {
        var payload = new PricedInPayload(
            "prompt", "usd", 1, 1_000_000, 3, 112_400, "anchor text", "source", null, "2025-01-01");

        var redacted = DailyRedaction.Redact(DailyGameKind.PricedIn, DailyJson.Serialize(payload)).AsObject();

        redacted.ContainsKey("trueValue").Should().BeFalse();
        redacted.ContainsKey("anchor").Should().BeFalse();
        redacted["minBound"]!.GetValue<double>().Should().Be(1);
    }

    [Fact]
    public void Redaction_PlaceIt_StripsTrueBucketRationaleAndEvidence()
    {
        var payload = new PlaceItPayload(
            Guid.NewGuid(), "title", "summary", "InCommittee",
            new List<PlaceItAxis> { new("authority", "Authority", "low", "high", 4, "because", "quote") },
            3);

        var redacted = DailyRedaction.Redact(DailyGameKind.PlaceIt, DailyJson.Serialize(payload));

        var axis = redacted["axes"]!.AsArray()[0]!.AsObject();
        axis.ContainsKey("trueBucket").Should().BeFalse();
        axis.ContainsKey("rationale").Should().BeFalse();
        axis.ContainsKey("evidence").Should().BeFalse();
        axis["name"]!.GetValue<string>().Should().Be("Authority");
    }

    [Fact]
    public void Redaction_TimeMachine_StripsOrderPickAndDates()
    {
        var payload = new TimeMachinePayload(
            TimeMachineMode.Sort,
            new List<TimeMachineItem> { new("a", "headline", "NYT") },
            new List<string> { "a" },
            "a",
            new Dictionary<string, string> { ["a"] = "1978-10-15" },
            new Dictionary<string, string> { ["a"] = "https://example.org" },
            "reveal");

        var redacted = DailyRedaction.Redact(DailyGameKind.TimeMachine, DailyJson.Serialize(payload)).AsObject();

        redacted.ContainsKey("trueOrder").Should().BeFalse();
        redacted.ContainsKey("currentItemId").Should().BeFalse();
        redacted.ContainsKey("dates").Should().BeFalse();
        // Publisher and URL are intentionally NOT secret.
        redacted["items"]!.AsArray()[0]!["publisher"]!.GetValue<string>().Should().Be("NYT");
    }

    [Fact]
    public void Redaction_WhoseValue_StripsCorrectAxisAndBillIdentity()
    {
        var payload = new WhoseValuePayload(new List<WhoseValueRound>
        {
            new("argument", "A Bill", Guid.NewGuid(),
                new List<WhoseValueChoice> { new("authority", "Authority", "low", "high") },
                "authority"),
        });

        var redacted = DailyRedaction.Redact(DailyGameKind.WhoseValue, DailyJson.Serialize(payload));

        var round = redacted["rounds"]!.AsArray()[0]!.AsObject();
        round.ContainsKey("correctAxisKey").Should().BeFalse();
        round.ContainsKey("billTitle").Should().BeFalse();
        round.ContainsKey("billId").Should().BeFalse();
        round["choices"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public void Redaction_Fork_HasNothingToHide()
    {
        DailyRedaction.PathsFor(DailyGameKind.Fork).Should().BeEmpty();
    }

    [Fact]
    public void Redaction_EveryKindHasAnExplicitPathList()
    {
        // A new game must consciously declare its secrets — a missing entry would throw
        // here rather than silently serving an answer key.
        foreach (var kind in Enum.GetValues<DailyGameKind>())
        {
            var act = () => DailyRedaction.PathsFor(kind);
            act.Should().NotThrow($"{kind} must declare its secret fields");
        }
    }

    // ------------------------------------------------------------ Leak filter

    [Theory]
    [InlineData("The bill centralizes permitting in a federal office.")]   // "centra" stem
    [InlineData("A decentralized approach is preserved for local roads.")] // "decent" stem
    [InlineData("This shifts authority away from the states.")]            // axis name stem
    public void LeakFilter_RejectsRationalesThatNameTheirOwnAxis(string rationale)
    {
        AxisLeakFilter.Leaks(rationale, "Authority", "Decentralized", "Centralized")
            .Should().BeTrue();
    }

    [Fact]
    public void LeakFilter_AllowsARationaleThatArguesWithoutNamingTheAxis()
    {
        const string rationale =
            "One standard instead of fifty means a project doesn't die in the gaps between them.";

        AxisLeakFilter.Leaks(rationale, "Authority", "Decentralized", "Centralized")
            .Should().BeFalse();
    }

    [Fact]
    public void LeakFilter_IgnoresWordsTooCommonInCivicWritingToBeATell()
    {
        // "government" appears in nearly every bill rationale, so matching on it would
        // reject the corpus while giving nothing away about WHICH axis is the answer.
        const string rationale = "The government would fund the pilot for three years.";

        AxisLeakFilter.Leaks(rationale, "Government role", "Minimal state", "Active public builder")
            .Should().BeFalse();
    }

    [Fact]
    public void LeakFilter_StemsExcludeShortAndCommonWords()
    {
        var stems = AxisLeakFilter.Stems("Government role", "Minimal state", "Active public builder");

        stems.Should().NotContain("govern");   // stoplisted
        stems.Should().NotContain("role");     // too short
        stems.Should().NotContain("public");   // stoplisted
        stems.Should().Contain("minima");      // genuinely distinctive
    }

    // -------------------------------------------------------- Fork validator

    private static ForkPayload Fork(string aCost = "costs something", string bCost = "costs something else",
        string question = "Who pays?") =>
        new(question, "a real tradeoff",
            new ForkOption("Option A", aCost), new ForkOption("Option B", bCost),
            "economic-fairness", "key", null);

    [Fact]
    public void ForkValidator_AcceptsATradeoffWhereBothOptionsCostSomething()
    {
        ForkValidator.IsAcceptable(Fork(), out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("", "costs something else")]
    [InlineData("costs something", "")]
    [InlineData("   ", "costs something else")]
    public void ForkValidator_RejectsAnOptionWithNoStatedCost(string aCost, string bCost)
    {
        // This is the whole neutrality guard: if we can't say what an option costs you,
        // it isn't a tradeoff, it's an applause line.
        ForkValidator.IsAcceptable(Fork(aCost, bCost), out var reason).Should().BeFalse();
        reason.Should().Contain("cost");
    }

    [Theory]
    [InlineData("Obviously the state should pay")]
    [InlineData("Should Republicans decide this?")]
    [InlineData("Stop the extremist proposal")]
    public void ForkValidator_RejectsPartisanOrEditorializingLanguage(string question)
    {
        ForkValidator.IsAcceptable(Fork(question: question), out var reason).Should().BeFalse();
        reason.Should().Contain("disqualifying");
    }

    // ------------------------------------------------------------ Seed banks

    [Fact]
    public void ForkFallbackBank_EveryItemPassesTheValidator()
    {
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.fork-fallback.json");

        bank.Should().NotBeNull();
        bank!.Should().HaveCountGreaterThanOrEqualTo(10);

        foreach (var item in bank)
        {
            var payload = new ForkPayload(
                item["question"]!.GetValue<string>(),
                item["tradeoff"]!.GetValue<string>(),
                new ForkOption(item["optionA"]!["label"]!.GetValue<string>(),
                               item["optionA"]!["cost"]!.GetValue<string>()),
                new ForkOption(item["optionB"]!["label"]!.GetValue<string>(),
                               item["optionB"]!["cost"]!.GetValue<string>()),
                item["axisKey"]!.GetValue<string>(),
                item["key"]!.GetValue<string>(),
                null);

            ForkValidator.IsAcceptable(payload, out var reason)
                .Should().BeTrue($"seed fork \"{payload.SubQuestionKey}\" should be shippable — {reason}");
        }
    }

    [Fact]
    public void ForkFallbackBank_EveryAxisKeyExistsInTheCatalog()
    {
        var catalog = new CivicCatalog();
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.fork-fallback.json")!;

        foreach (var item in bank)
        {
            var axisKey = item["axisKey"]!.GetValue<string>();
            catalog.AxisFor(axisKey).Should().NotBeNull($"\"{axisKey}\" must be a real axis");
        }
    }

    [Fact]
    public void ForkFallbackBank_KeysAreUnique()
    {
        var keys = SeedService.LoadJson<List<JsonObject>>("Seed.fork-fallback.json")!
            .Select(i => i["key"]!.GetValue<string>()).ToList();

        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MagnitudeBank_IsBalancedBetweenSmallerAndBiggerThanYouThink()
    {
        // A bank stacked with "much smaller than you think" answers argues a thesis,
        // whichever thesis that happens to be. Keep it within 55/45.
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.magnitudes.json")!
            .Where(i => i["verified"]!.GetValue<bool>()).ToList();

        bank.Should().NotBeEmpty();

        var smaller = bank.Count(i => i["direction"]!.GetValue<string>() == "smaller");
        var share = (double)smaller / bank.Count;

        share.Should().BeInRange(0.45, 0.55,
            "the magnitude bank must not lean toward one direction of surprise");
    }

    [Fact]
    public void MagnitudeBank_EveryVerifiedItemCarriesProvenance()
    {
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.magnitudes.json")!;

        foreach (var item in bank.Where(i => i["verified"]!.GetValue<bool>()))
        {
            item["source"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            item["asOf"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            item["trueValue"]!.GetValue<double>().Should().BeGreaterThan(0);
            item["minBound"]!.GetValue<double>().Should()
                .BeLessThan(item["trueValue"]!.GetValue<double>());
            item["maxBound"]!.GetValue<double>().Should()
                .BeGreaterThan(item["trueValue"]!.GetValue<double>());
        }
    }

    [Fact]
    public void CrowdCallPollBank_PlaceholderRowsAreFlaggedUnverified()
    {
        // Placeholder rows exist so the game is playable on a dev box. They carry made-up
        // rates, so they must never be marked verified — the generator only ships verified
        // rows outside Development.
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.crowd-call-polls.json")!;

        foreach (var item in bank)
        {
            var attribution = item["attribution"]!.GetValue<string>();
            if (attribution.StartsWith("PLACEHOLDER"))
                item["verified"]!.GetValue<bool>().Should().BeFalse(
                    "a placeholder figure must never be shippable");
        }
    }

    [Fact]
    public void ArchiveHeadlineBank_EveryItemIsRealAndCitable()
    {
        // Ships empty on purpose — the contents must be real, human-verified headlines.
        // This test is the guard for when someone populates it.
        var bank = SeedService.LoadJson<List<JsonObject>>("Seed.archive-headlines.json")!;

        foreach (var item in bank)
        {
            item["headline"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            item["publisher"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            item["url"]!.GetValue<string>().Should().StartWith("http");
            item["publishedAt"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        }
    }
}
