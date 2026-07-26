using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Daily;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// End-to-end coverage of the daily-games API: the platform guarantees (anonymous play,
/// answer-key hygiene, one play per person, XP awarded exactly once) plus one full
/// play-through per game kind.
///
/// Puzzles are inserted directly rather than generated, so each test controls its own
/// content — the generation host is disabled for the suite (see TestHostConfig).
/// </summary>
[Collection("Database")]
public class DailyApiTests
{
    private readonly DatabaseFixture _fx;

    public DailyApiTests(DatabaseFixture fx) => _fx = fx;

    private HttpClient ClientFor(string userId)
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        return client;
    }

    /// <summary>A client with no identity at all — resolves to the literal "anonymous".</summary>
    private HttpClient AnonymousClient() => _fx.Factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Insert a live puzzle for today. Returns it.</summary>
    private async Task<DailyPuzzle> SeedPuzzleAsync(DailyGameKind kind, object payload, DateOnly? date = null)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await db.DailyPuzzles
            .FirstOrDefaultAsync(p => p.Kind == kind && p.PuzzleDate == day && p.Locality == null);
        if (existing is not null)
        {
            db.DailyPuzzles.Remove(existing);
            await db.SaveChangesAsync();
        }

        var puzzle = new DailyPuzzle
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            PuzzleDate = day,
            Edition = 1,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            Status = DailyPuzzleStatus.Live,
            GenerationSource = CivicGenerationSource.Seed,
        };
        db.DailyPuzzles.Add(puzzle);
        await db.SaveChangesAsync();
        return puzzle;
    }

    // ------------------------------------------------------------ payloads

    private static object ForkPayload() => new
    {
        question = "Who pays for the grid upgrade?",
        tradeoff = "Charging the facility slows buildout; spreading it raises bills.",
        optionA = new { label = "The facility pays", cost = "Fewer facilities get built." },
        optionB = new { label = "All ratepayers share", cost = "Your bill goes up." },
        axisKey = "economic-fairness",
        subQuestionKey = "cost-allocation",
        provisionSlug = "grid-fee",
    };

    private static object CrowdCallPayload() => new
    {
        rounds = new[]
        {
            new
            {
                prompt = "Which branch can declare a law unconstitutional?",
                answer = "The judicial branch",
                explanation = "Marbury v. Madison (1803).",
                crowdSource = "civic-users",
                attribution = "Civersify players, last 60 days",
                sourceUrl = (string?)null,
                fieldedOn = (string?)null,
                sampleSize = 412,
                trueRate = 0.68,
            },
        },
    };

    private static object PricedInPayload() => new
    {
        prompt = "How big is this?",
        unit = "usd",
        minBound = 1_000d,
        maxBound = 1_000_000d,
        maxGuesses = 3,
        trueValue = 100_000d,
        anchor = "About a tenth of the ceiling.",
        source = "Test fixture",
        sourceUrl = (string?)null,
        asOf = "2025-01-01",
    };

    private static object PlaceItPayload() => new
    {
        billId = Guid.NewGuid(),
        billTitle = "A Test Bill",
        billSummary = "It does a thing.",
        billStatus = "InCommittee",
        axes = new[]
        {
            new { axisKey = "authority", name = "Authority", lowLabel = "Decentralized", highLabel = "Centralized", trueBucket = 4, rationale = "Moves power up.", evidence = (string?)null },
            new { axisKey = "risk", name = "Risk", lowLabel = "Precautionary", highLabel = "Innovation-tolerant", trueBucket = 1, rationale = "Adds review.", evidence = (string?)null },
            new { axisKey = "speech", name = "Speech", lowLabel = "Open expression", highLabel = "Harm-aware moderation", trueBucket = 2, rationale = "Neutral here.", evidence = (string?)null },
        },
        maxRounds = 3,
    };

    private static object TimeMachinePayload() => new
    {
        mode = "sort",
        items = new[]
        {
            new { id = "b", headline = "Second story", publisher = "NPR" },
            new { id = "a", headline = "First story", publisher = "AP" },
            new { id = "c", headline = "Third story", publisher = "Reuters" },
        },
        trueOrder = new[] { "a", "b", "c" },
        currentItemId = (string?)null,
        dates = new Dictionary<string, string> { ["a"] = "1978-01-01", ["b"] = "1991-01-01", ["c"] = "2011-01-01" },
        urls = new Dictionary<string, string> { ["a"] = "https://example.org/a", ["b"] = "https://example.org/b", ["c"] = "https://example.org/c" },
        revealLine = "Older than it looks.",
    };

    private static object WhoseValuePayload() => new
    {
        rounds = new[]
        {
            new
            {
                argument = "One standard instead of fifty keeps projects from dying in the gaps.",
                billTitle = "A Test Bill",
                billId = Guid.NewGuid(),
                choices = new[]
                {
                    new { axisKey = "authority", name = "Authority", lowLabel = "Decentralized", highLabel = "Centralized" },
                    new { axisKey = "risk", name = "Risk", lowLabel = "Precautionary", highLabel = "Innovation-tolerant" },
                },
                correctAxisKey = "authority",
            },
        },
    };

    // -------------------------------------------------- platform guarantees

    [Fact]
    public async Task Slate_IsServedToAFullyAnonymousCaller()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        var slate = await AnonymousClient().GetFromJsonAsync<DailySlateDto>("/api/daily");

        slate.Should().NotBeNull();
        slate!.Anonymous.Should().BeTrue();
        slate.Puzzles.Should().Contain(p => p.Kind == "Fork");
    }

    [Fact]
    public async Task Slate_DegradesToHoweverManyGamesAreLive_NeverErrors()
    {
        await _fx.ResetMutableAsync();
        // Nothing seeded at all.
        var resp = await AnonymousClient().GetAsync("/api/daily");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var slate = await resp.Content.ReadFromJsonAsync<DailySlateDto>();
        slate!.Puzzles.Should().BeEmpty();
    }

    [Fact]
    public async Task Slate_OnlyIncludesLivePuzzles_DraftsAreInvisible()
    {
        await _fx.ResetMutableAsync();
        var puzzle = await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            var row = await db.DailyPuzzles.FirstAsync(p => p.Id == puzzle.Id);
            row.Status = DailyPuzzleStatus.Draft;
            await db.SaveChangesAsync();
        }

        var slate = await AnonymousClient().GetFromJsonAsync<DailySlateDto>("/api/daily");

        slate!.Puzzles.Should().BeEmpty();
    }

    [Fact]
    public async Task AnonymousPlay_Works_ButRecordsNoLedgerRowAndNoPlay()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        var result = await (await AnonymousClient()
            .PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result.Should().NotBeNull();
        result!.Completed.Should().BeTrue();
        // No stable id → nothing recorded. Pooling every anonymous visitor into one
        // "anonymous" bucket would corrupt the XP curve and the cohort board.
        result.PointsAwarded.Should().Be(0);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.CoalitionActs.CountAsync(a => a.Type == CoalitionActType.DailyPuzzle)).Should().Be(0);
        (await db.DailyPuzzlePlays.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IdentifiedPlay_AwardsXpOnce_AndLogsAnActivityDay()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.CrowdCall, CrowdCallPayload());
        var userId = Guid.NewGuid().ToString();

        var result = await (await ClientFor(userId)
            .PostAsJsonAsync("/api/daily/crowd-call/plays", new { guesses = new[] { 0.68 } }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result!.Score.Should().Be(100);
        result.PointsAwarded.Should().BeGreaterThan(0);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.CoalitionActs.CountAsync(a => a.UserId == userId && a.Type == CoalitionActType.DailyPuzzle))
            .Should().Be(1);
        // The shared activity-day table is what powers the weekly ring — for free.
        (await db.CoalitionActivityDays.CountAsync(a => a.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task Replay_IsRejectedWithConflict_AndDoesNotDoubleAward()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        var first = await client.PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/daily/fork/plays", new { choice = "B" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.CoalitionActs.CountAsync(a => a.UserId == userId && a.Type == CoalitionActType.DailyPuzzle))
            .Should().Be(1);
        (await db.DailyPuzzlePlays.CountAsync(p => p.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task PlayingEveryGame_StaysUnderTheDailyCapWithDiminishingReturns()
    {
        await _fx.ResetMutableAsync();
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());
        await SeedPuzzleAsync(DailyGameKind.CrowdCall, CrowdCallPayload());
        await SeedPuzzleAsync(DailyGameKind.TimeMachine, TimeMachinePayload());
        await SeedPuzzleAsync(DailyGameKind.WhoseValue, WhoseValuePayload());

        // Two deliberately WRONG plays, so neither earns the accuracy bonus and the only
        // thing separating them is the 0.8x within-day decay. (Comparing a wrong play to
        // a perfect one wouldn't isolate the decay — the bonus can outweigh it, which is
        // fine: the daily cap, not monotonic decay, is the real bound.)
        var firstWrong = (await (await client.PostAsJsonAsync(
            "/api/daily/fork/plays", new { choice = "A" }))
            .Content.ReadFromJsonAsync<DailyResultDto>())!;
        var secondWrong = (await (await client.PostAsJsonAsync(
            "/api/daily/time-machine/plays", new { order = new[] { "c", "b", "a" } }))
            .Content.ReadFromJsonAsync<DailyResultDto>())!;

        firstWrong.Score.Should().Be(0);
        secondWrong.Score.Should().Be(0);
        secondWrong.PointsAwarded.Should().BeLessThan(firstWrong.PointsAwarded,
            "the second reasoning act of the day earns 0.8x the first");

        // Then the rest of the slate, to confirm the cap holds across a full sweep.
        await client.PostAsJsonAsync("/api/daily/crowd-call/plays", new { guesses = new[] { 0.68 } });
        await client.PostAsJsonAsync("/api/daily/whose-value/plays", new { picks = new[] { "authority" } });

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var earned = await db.CoalitionActs
            .Where(a => a.UserId == userId && a.Currency == "reasoning")
            .SumAsync(a => a.Points);

        // Playing the whole slate is worth a rounding error next to real coalition work.
        earned.Should().BeLessThanOrEqualTo(150);
        earned.Should().BeLessThan(20);
    }

    [Fact]
    public async Task DailyGamePlayer_AppearsOnTheCohortBoard()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());
        var userId = Guid.NewGuid().ToString();
        var client = ClientFor(userId);

        await client.PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" });

        // CohortService ranks the ledger, not coalition acts specifically — so a player
        // who has only played daily games shows up with no extra wiring.
        var cohort = await client.GetAsync("/api/cohort/me");
        cohort.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await cohort.Content.ReadAsStringAsync();
        body.Should().Contain(userId);
    }

    [Fact]
    public async Task Cadence_ReflectsTodaysPlay()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());
        var client = ClientFor(Guid.NewGuid().ToString());

        await client.PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" });
        var slate = await client.GetFromJsonAsync<DailySlateDto>("/api/daily");

        slate!.Cadence.ActiveDays.Should().Be(1);
        slate.Cadence.Last7Days.Should().HaveCount(7);
        slate.Cadence.Last7Days[6].Should().BeTrue("today is the last entry");
    }

    [Fact]
    public async Task UnknownKind_Returns404()
    {
        (await AnonymousClient().GetAsync("/api/daily/not-a-game")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Play_WithNoLivePuzzle_Returns404()
    {
        await _fx.ResetMutableAsync();

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Play_WithMalformedBody_Returns400NotAServerError()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/fork/plays", new { choice = "Z" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LadderGames_RejectTheSingleShotEndpoint()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PlaceIt, PlaceItPayload());

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/place-it/plays", new { guesses = new[] { 1, 1, 1 } });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------ answer-key hygiene

    [Theory]
    [InlineData(DailyGameKind.CrowdCall, "crowd-call", "trueRate")]
    [InlineData(DailyGameKind.CrowdCall, "crowd-call", "sampleSize")]
    [InlineData(DailyGameKind.PricedIn, "priced-in", "trueValue")]
    [InlineData(DailyGameKind.PricedIn, "priced-in", "anchor")]
    [InlineData(DailyGameKind.PlaceIt, "place-it", "trueBucket")]
    [InlineData(DailyGameKind.PlaceIt, "place-it", "rationale")]
    [InlineData(DailyGameKind.TimeMachine, "time-machine", "trueOrder")]
    [InlineData(DailyGameKind.TimeMachine, "time-machine", "dates")]
    [InlineData(DailyGameKind.WhoseValue, "whose-value", "correctAxisKey")]
    public async Task Get_NeverLeaksAnAnswerKeyField(DailyGameKind kind, string slug, string secretField)
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(kind, kind switch
        {
            DailyGameKind.CrowdCall => CrowdCallPayload(),
            DailyGameKind.PricedIn => PricedInPayload(),
            DailyGameKind.PlaceIt => PlaceItPayload(),
            DailyGameKind.TimeMachine => TimeMachinePayload(),
            _ => WhoseValuePayload(),
        });

        var single = await AnonymousClient().GetStringAsync($"/api/daily/{slug}");
        var slate = await AnonymousClient().GetStringAsync("/api/daily");

        single.Should().NotContain(secretField);
        slate.Should().NotContain(secretField);
    }

    // ----------------------------------------------------- per-game flows

    [Fact]
    public async Task Fork_RevealsTheSplitAndSuppressesAThinSample()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        var result = await (await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result!.Score.Should().Be(0, "Fork has no right answer");
        var national = result.Crowd!["national"]!;
        // One play is not a national split — it must be suppressed, not shown as 100%.
        national["suppressed"]!.GetValue<bool>().Should().BeTrue();
        national["total"]!.GetValue<int>().Should().Be(1);
        result.ShareGrid.Should().Contain("Fork #1").And.Contain("civersify.com/daily");
    }

    [Fact]
    public async Task Fork_ShareGridNeverLeaksTheOtherOptionsText()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        var result = await (await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/fork/plays", new { choice = "A" }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result!.ShareGrid.Should().NotContain("The facility pays");
        result.ShareGrid.Should().NotContain("Who pays for the grid");
    }

    [Fact]
    public async Task CrowdCall_ScoresCalibrationAndRevealsTheTrueRate()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.CrowdCall, CrowdCallPayload());

        var result = await (await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/crowd-call/plays", new { guesses = new[] { 0.40 } }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        // 28 points of error → 100 - 2*28 = 44.
        result!.Score.Should().Be(44);
        result.Reveal!["rounds"]![0]!["trueRate"]!.GetValue<double>().Should().Be(0.68);
        result.Reveal["overestimatedDivision"]!.GetValue<int>().Should().Be(1);
        result.ShareGrid.Should().Contain("Crowd Call #1");
    }

    [Fact]
    public async Task PricedIn_LadderGivesDirectionOnlyUntilTheReveal()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PricedIn, PricedInPayload());
        var client = ClientFor(Guid.NewGuid().ToString());

        var first = await (await client.PostAsJsonAsync(
            "/api/daily/priced-in/guesses", new { guess = 10_000d, final = false }))
            .Content.ReadFromJsonAsync<PricedInGuessResultDto>();

        first!.Completed.Should().BeFalse();
        first.Direction.Should().Be("higher");
        first.GuessesRemaining.Should().Be(2);
        // Mid-ladder responses must not carry the answer.
        first.Result.Should().BeNull();

        var final = await (await client.PostAsJsonAsync(
            "/api/daily/priced-in/guesses", new { guess = 100_000d, final = true }))
            .Content.ReadFromJsonAsync<PricedInGuessResultDto>();

        final!.Completed.Should().BeTrue();
        final.Result!.Score.Should().Be(90, "exact on the second guess takes a 10% haircut");
        final.Result.Reveal!["trueValue"]!.GetValue<double>().Should().Be(100_000);
        final.Result.PointsAwarded.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PricedIn_RunsOutOfGuessesAndCompletesAnyway()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PricedIn, PricedInPayload());
        var client = ClientFor(Guid.NewGuid().ToString());

        PricedInGuessResultDto? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await (await client.PostAsJsonAsync(
                "/api/daily/priced-in/guesses", new { guess = 5_000d, final = false }))
                .Content.ReadFromJsonAsync<PricedInGuessResultDto>();
        }

        last!.Completed.Should().BeTrue();
        last.GuessesUsed.Should().Be(3);
        last.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task PricedIn_RejectsANonPositiveGuess()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PricedIn, PricedInPayload());

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/priced-in/guesses", new { guess = 0d, final = true });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlaceIt_LadderHintsThenRevealsRationales()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PlaceIt, PlaceItPayload());
        var client = ClientFor(Guid.NewGuid().ToString());

        var round1 = await (await client.PostAsJsonAsync(
            "/api/daily/place-it/rounds", new { guesses = new[] { 2, 2, 2 } }))
            .Content.ReadFromJsonAsync<PlaceItRoundResultDto>();

        round1!.Completed.Should().BeFalse();
        round1.Hints.Should().Equal("higher", "lower", "exact");
        round1.RoundsRemaining.Should().Be(2);
        round1.Result.Should().BeNull();

        var round2 = await (await client.PostAsJsonAsync(
            "/api/daily/place-it/rounds", new { guesses = new[] { 4, 1, 2 } }))
            .Content.ReadFromJsonAsync<PlaceItRoundResultDto>();

        round2!.Completed.Should().BeTrue("all three axes are exact");
        round2.Result!.Score.Should().Be(85, "100 with a 15% haircut for the second round");
        round2.Result.Reveal!["axes"]![0]!["rationale"]!.GetValue<string>().Should().Be("Moves power up.");
        round2.Result.ShareGrid.Should().Contain("Place It #1");
    }

    [Fact]
    public async Task PlaceIt_ExhaustsItsRoundsAndScoresTheLastAttempt()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PlaceIt, PlaceItPayload());
        var client = ClientFor(Guid.NewGuid().ToString());

        PlaceItRoundResultDto? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await (await client.PostAsJsonAsync(
                "/api/daily/place-it/rounds", new { guesses = new[] { 0, 0, 0 } }))
                .Content.ReadFromJsonAsync<PlaceItRoundResultDto>();
        }

        last!.Completed.Should().BeTrue();
        last.RoundsUsed.Should().Be(3);
        last.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task PlaceIt_RejectsOutOfRangeBuckets()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PlaceIt, PlaceItPayload());

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/place-it/rounds", new { guesses = new[] { 9, 1, 2 } });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlaceIt_RejectsTheWrongNumberOfGuesses()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.PlaceIt, PlaceItPayload());

        var resp = await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/place-it/rounds", new { guesses = new[] { 1, 2 } });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TimeMachine_ScoresPairwiseAndRevealsDates()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.TimeMachine, TimeMachinePayload());

        var result = await (await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/time-machine/plays", new { order = new[] { "a", "b", "c" } }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result!.Score.Should().Be(100);
        result.Reveal!["dates"]!["a"]!.GetValue<string>().Should().Be("1978-01-01");
        result.ShareGrid.Should().Contain("Time Machine #1").And.Contain("3/3 pairs");
        // The grid must not contain any headline text.
        result.ShareGrid.Should().NotContain("First story");
    }

    [Fact]
    public async Task WhoseValue_ScoresAndNamesTheAxisInTheReveal()
    {
        await _fx.ResetMutableAsync();
        await SeedPuzzleAsync(DailyGameKind.WhoseValue, WhoseValuePayload());

        var result = await (await ClientFor(Guid.NewGuid().ToString())
            .PostAsJsonAsync("/api/daily/whose-value/plays", new { picks = new[] { "authority" } }))
            .Content.ReadFromJsonAsync<DailyResultDto>();

        result!.Score.Should().Be(100);
        result.Reveal!["rounds"]![0]!["correctAxisKey"]!.GetValue<string>().Should().Be("authority");
        result.Reveal["sharpestAxisName"]!.GetValue<string>().Should().Be("Authority");
    }

    // --------------------------------------------------------- review queue

    /// <summary>Admin gate is an email allowlist — appsettings.Development lists admin@arena.local.</summary>
    private HttpClient AdminClient()
    {
        var client = _fx.Factory.CreateClient();
        var token = JwtTestHelper.MintAccessToken(Guid.NewGuid(), email: "admin@arena.local");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ReviewQueue_IsAdminOnly()
    {
        (await AnonymousClient().GetAsync("/api/admin/daily")).StatusCode
            .Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var nonAdmin = _fx.Factory.CreateClient();
        nonAdmin.DefaultRequestHeaders.Authorization =
            new("Bearer", JwtTestHelper.MintAccessToken(Guid.NewGuid(), email: "nobody@example.com"));
        (await nonAdmin.GetAsync("/api/admin/daily")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReviewQueue_ApprovingADraftMakesItPlayable()
    {
        await _fx.ResetMutableAsync();
        var puzzle = await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());

        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            (await db.DailyPuzzles.FirstAsync(p => p.Id == puzzle.Id)).Status = DailyPuzzleStatus.Draft;
            await db.SaveChangesAsync();
        }

        // Invisible to players while it's a draft.
        (await AnonymousClient().GetFromJsonAsync<DailySlateDto>("/api/daily"))!
            .Puzzles.Should().BeEmpty();

        var admin = AdminClient();
        var drafts = await admin.GetFromJsonAsync<List<AdminDailyPuzzleDto>>("/api/admin/daily?status=Draft");
        drafts.Should().ContainSingle(d => d.Id == puzzle.Id);
        // The reviewer's view deliberately DOES include the payload in full.
        drafts![0].Payload.Should().NotBeNull();

        (await admin.PostAsync($"/api/admin/daily/{puzzle.Id}/approve", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await AnonymousClient().GetFromJsonAsync<DailySlateDto>("/api/daily"))!
            .Puzzles.Should().ContainSingle(p => p.Kind == "Fork");
    }

    [Fact]
    public async Task ReviewQueue_RejectingRetiresRatherThanDeletes()
    {
        await _fx.ResetMutableAsync();
        var puzzle = await SeedPuzzleAsync(DailyGameKind.Fork, ForkPayload());
        var admin = AdminClient();

        await admin.PostAsync($"/api/admin/daily/{puzzle.Id}/reject", null);

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var row = await db.DailyPuzzles.FirstOrDefaultAsync(p => p.Id == puzzle.Id);

        // Retained so the generator's "don't reuse this source" checks still see it and
        // won't re-cut the same puzzle from the same content tomorrow.
        row.Should().NotBeNull();
        row!.Status.Should().Be(DailyPuzzleStatus.Retired);

        (await AnonymousClient().GetFromJsonAsync<DailySlateDto>("/api/daily"))!
            .Puzzles.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewQueue_BalanceReportsTheMagnitudeBankSkew()
    {
        var balance = await AdminClient().GetFromJsonAsync<AdminDailyBalanceDto>("/api/admin/daily/balance");

        balance.Should().NotBeNull();
        balance!.MagnitudeTotal.Should().BeGreaterThan(0);
        balance.MagnitudeSmallerShare.Should().BeInRange(0.45, 0.55);
    }

    [Fact]
    public async Task ReviewQueue_UnknownPuzzle_Returns404()
    {
        (await AdminClient().PostAsync($"/api/admin/daily/{Guid.NewGuid()}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------- archive

    [Fact]
    public async Task Archive_ReportsThisPlayersPastScores()
    {
        await _fx.ResetMutableAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await SeedPuzzleAsync(DailyGameKind.CrowdCall, CrowdCallPayload(), yesterday);
        var client = ClientFor(Guid.NewGuid().ToString());

        await client.PostAsJsonAsync(
            $"/api/daily/crowd-call/plays?date={yesterday:yyyy-MM-dd}",
            new { guesses = new[] { 0.68 } });

        var rows = await client.GetFromJsonAsync<List<DailyArchiveRowDto>>("/api/daily/crowd-call/archive");

        rows.Should().ContainSingle();
        rows![0].Played.Should().BeTrue();
        rows[0].Score.Should().Be(100);
    }
}
