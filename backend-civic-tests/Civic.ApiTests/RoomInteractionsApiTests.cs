using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Civic.API.Controllers.Api;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Civic.API.Services.Rooms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Room interactions and calibrated predictions over the wire.
///
/// The two things worth testing at this level rather than in unit tests are the ones a pure
/// function cannot prove: that the answer key never reaches the client, and that no endpoint
/// returns another person's forecast.
/// </summary>
[Collection("Database")]
public class RoomInteractionsApiTests
{
    private readonly DatabaseFixture _fx;

    public RoomInteractionsApiTests(DatabaseFixture fx) => _fx = fx;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private HttpClient Anonymous() => _fx.Factory.CreateClient();

    private HttpClient Verified(string subject)
    {
        var client = _fx.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTestHelper.MintAccessToken(subject));
        return client;
    }

    private async Task<(string RoomSlug, string InteractionSlug)> SeedClassifyAsync()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var room = new ThemeRoom
        {
            Id = Guid.NewGuid(),
            Slug = "interaction-room",
            Title = "Interaction room",
            Dek = "A room with an interaction attached.",
            Status = RoomStatus.Published,
        };
        db.Rooms.Add(room);

        var payload = new ClassifyStatementPayload(new List<ClassifyItem>
        {
            new("1", "The fiscal year begins October 1.", "Factual", "Set in statute."),
            new("2", "This is the worst budget in a decade.", "Opinion", "A value judgement."),
        });

        db.Interactions.Add(new Interaction
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            Slug = "sort-the-sentences",
            Kind = InteractionKind.ClassifyStatement,
            Title = "Sort the sentences",
            Prompt = "Label each sentence.",
            PayloadJson = InteractionJson.Serialize(payload),
            Explanation = "Facts can be checked; opinions cannot.",
            ScoringMode = InteractionScoringMode.Partial,
            Status = RoomStatus.Published,
        });

        await db.SaveChangesAsync();
        return (room.Slug, "sort-the-sentences");
    }

    private async Task<string> SeedPredictionAsync(
        DateTime? closesAt = null, PredictionOutcome outcome = PredictionOutcome.Unresolved)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        var p = new Prediction
        {
            Id = Guid.NewGuid(),
            Slug = "cloture-by-friday",
            Proposition = "The Senate invokes cloture before Friday.",
            ResolutionCriteria = "A recorded cloture vote succeeding before 23:59 ET Friday.",
            ResolutionSourceDescription = "The Senate roll call record.",
            CancellationPolicy = "Cancelled if the bill is withdrawn.",
            ClosesAt = closesAt ?? DateTime.UtcNow.AddDays(3),
            Outcome = outcome,
            Status = RoomStatus.Published,
        };
        db.Predictions.Add(p);
        await db.SaveChangesAsync();
        return p.Slug;
    }

    // ------------------------------------------------------------------ answer keys

    [Fact]
    public async Task TheAnswerKey_NeverReachesTheClient()
    {
        // Asserted against the RAW response body, not a deserialized DTO. A DTO that
        // happens to omit a field proves nothing about what went over the wire.
        await _fx.ResetMutableAsync();
        var (roomSlug, _) = await SeedClassifyAsync();

        var res = await Anonymous().GetAsync($"/api/rooms/{roomSlug}/interactions");
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("The fiscal year begins October 1.", "the prompt is public");
        body.Should().NotContain("correctLabel");
        body.Should().NotContain("Set in statute.", "explanations are withheld until answered");
    }

    [Fact]
    public async Task SubmittingReturnsAnExplanationForEveryItem()
    {
        await _fx.ResetMutableAsync();
        var (roomSlug, slug) = await SeedClassifyAsync();

        var res = await Anonymous().PostAsJsonAsync(
            $"/api/rooms/{roomSlug}/interactions/{slug}/submit",
            new { responseJson = "{\"labels\":{\"1\":\"Factual\",\"2\":\"Prediction\"}}" });

        var dto = await res.Content.ReadFromJsonAsync<SubmitResultDto>(Json);

        dto!.Score.Should().Be(50);
        dto.Items.Should().HaveCount(2);
        dto.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Explanation));
    }

    [Fact]
    public async Task AnonymousPlay_StoresNothingAndEarnsNothing()
    {
        await _fx.ResetMutableAsync();
        var (roomSlug, slug) = await SeedClassifyAsync();

        var res = await Anonymous().PostAsJsonAsync(
            $"/api/rooms/{roomSlug}/interactions/{slug}/submit",
            new { responseJson = "{\"labels\":{\"1\":\"Factual\"}}" });

        var dto = await res.Content.ReadFromJsonAsync<SubmitResultDto>(Json);
        dto!.Persisted.Should().BeFalse();

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        (await db.RoomInteractionPlays.CountAsync()).Should().Be(0);
        (await db.CoalitionActs.CountAsync(a => a.Type == CoalitionActType.RoomInteraction))
            .Should().Be(0);
    }

    [Fact]
    public async Task XpIsAwardedExactlyOncePerInteraction()
    {
        await _fx.ResetMutableAsync();
        var (roomSlug, slug) = await SeedClassifyAsync();
        var client = Verified("player-1");

        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync(
                $"/api/rooms/{roomSlug}/interactions/{slug}/submit",
                new { responseJson = "{\"labels\":{\"1\":\"Factual\",\"2\":\"Opinion\"}}" });
        }

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        (await db.CoalitionActs.CountAsync(a =>
            a.UserId == "player-1" && a.Type == CoalitionActType.RoomInteraction))
            .Should().Be(1);
    }

    // ------------------------------------------------------------------ predictions

    [Fact]
    public async Task Forecasting_RequiresAnAccount()
    {
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        var res = await Anonymous().PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 70 });

        res.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AForecastIsRecordedAndReturnedToItsOwner()
    {
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        var res = await Verified("forecaster-1").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 70 });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await res.Content.ReadFromJsonAsync<PredictionDto>(Json);
        dto!.MyProbability.Should().Be(70);
    }

    [Fact]
    public async Task NoEndpointReturnsAnotherPersonsForecast()
    {
        // Privacy by absence: there is no endpoint that CAN return someone else's number,
        // so there is no flag to misconfigure.
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        await Verified("forecaster-a").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 90 });

        var res = await Verified("forecaster-b").GetAsync($"/api/predictions/{slug}");
        var body = await res.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<PredictionDto>(body, Json);

        dto!.MyProbability.Should().BeNull("forecaster-b has not answered");
        dto.ForecastCount.Should().Be(1);
        body.Should().NotContain("forecaster-a");
    }

    [Fact]
    public async Task TheCrowdFigureIsSuppressedUntilEnoughPeopleHaveAnswered()
    {
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        await Verified("forecaster-1").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 90 });

        var dto = await Anonymous().GetFromJsonAsync<PredictionDto>($"/api/predictions/{slug}", Json);

        dto!.CrowdMeanProbability.Should().BeNull();
        dto.CrowdSuppressedBelow.Should().Be(PredictionsController.MinCrowdForecasts,
            "the client is told why the number is missing rather than guessing");
    }

    [Fact]
    public async Task ForecastingClosesAtTheDeadline()
    {
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync(closesAt: DateTime.UtcNow.AddMinutes(-5));

        var res = await Verified("late-forecaster").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 60 });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResolutionCriteriaAndCancellationPolicy_AreStatedBeforeAnswering()
    {
        // Design 1v puts both above the slider. An API that withheld them would make that
        // promise unkeepable.
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        var dto = await Anonymous().GetFromJsonAsync<PredictionDto>($"/api/predictions/{slug}", Json);

        dto!.ResolutionCriteria.Should().NotBeNullOrWhiteSpace();
        dto.CancellationPolicy.Should().NotBeNullOrWhiteSpace();
        dto.ResolutionSourceDescription.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdatingAForecast_DoesNotPayXpTwice()
    {
        // Charging for changing your mind would discourage exactly the behaviour the
        // product exists to reward.
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();
        var client = Verified("mind-changer");

        await client.PostAsJsonAsync($"/api/predictions/{slug}/forecast", new { probability = 60 });
        await client.PostAsJsonAsync($"/api/predictions/{slug}/forecast", new { probability = 75 });

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();

        (await db.CoalitionActs.CountAsync(a =>
            a.UserId == "mind-changer" && a.Type == CoalitionActType.CalibratedForecast))
            .Should().Be(1);

        (await db.UserPredictions.FirstAsync(u => u.UserId == "mind-changer"))
            .Probability.Should().Be(75);
    }

    [Fact]
    public async Task Calibration_ReturnsOnlyTheCallersOwnData()
    {
        await _fx.ResetMutableAsync();
        var slug = await SeedPredictionAsync();

        await Verified("cal-a").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 80 });
        await Verified("cal-b").PostAsJsonAsync(
            $"/api/predictions/{slug}/forecast", new { probability = 20 });

        var dto = await Verified("cal-a")
            .GetFromJsonAsync<CalibrationDto>("/api/predictions/me/calibration", Json);

        dto!.TotalForecasts.Should().Be(1, "cal-b's forecast is not cal-a's business");
    }

    [Fact]
    public async Task ThereIsNoLeaderboardEndpoint()
    {
        // PRD 06 §7.5 forbids ranking forecasters. This is the regression guard against a
        // future "helpful" addition.
        await _fx.ResetMutableAsync();

        foreach (var path in new[]
        {
            "/api/predictions/leaderboard",
            "/api/predictions/rankings",
            "/api/predictions/top",
        })
        {
            (await Verified("curious").GetAsync(path)).StatusCode
                .Should().Be(HttpStatusCode.NotFound, "for {0}", path);
        }
    }
}
