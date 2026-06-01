using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Campaign;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Integration tests for the Campaign Manager game mode against the real civic_test DB and the
/// seeded 5-candidate President race. The service is constructed directly (like
/// CampaignGenerationApiTests) with a real scoped DbContext. The Anthropic key is empty in the
/// test environment, so post generation falls back to templated posts — no network is hit.
/// </summary>
[Collection("Database")]
public class CivicCampaignServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public CivicCampaignServiceTests(DatabaseFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Runs an action with a freshly-scoped CivicCampaignService + DbContext.</summary>
    private async Task<T> WithServiceAsync<T>(Func<CivicCampaignService, Task<T>> body)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var postGen = scope.ServiceProvider.GetRequiredService<CampaignPostGenerationService>();
        var svc = new CivicCampaignService(
            db,
            postGen,
            Options.Create(new CivicCampaignOptions
            {
                // Deterministic opponents for assertions.
                OpponentVariance = 0,
            }),
            NullLogger<CivicCampaignService>.Instance);
        return await body(svc);
    }

    private static CreateCivicCampaignRequest CreateReq(int weeks = 4, CivicCampaignDifficulty diff = CivicCampaignDifficulty.Normal) => new()
    {
        CandidateSlug = "sofia-alvarez",
        Difficulty = diff,
        TotalWeeks = weeks,
    };

    [Fact]
    public async Task GetRaces_ReturnsSeededPresidentRaceWithMultipleCandidates()
    {
        var races = await WithServiceAsync(s => s.GetRacesAsync());
        races.Should().NotBeEmpty();
        var president = races.Should().ContainSingle(r => r.Office == "President").Subject;
        president.Candidates.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Create_SeedsStandingsThatSumToOneHundred()
    {
        var detail = await WithServiceAsync(s => s.CreateAsync("user-1", CreateReq()));

        detail.Standings.Should().HaveCountGreaterThan(1);
        detail.Standings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
        detail.Standings.Should().ContainSingle(s => s.IsPlayer);
        detail.CurrentWeek.Should().Be(1);
        detail.ActionsRemaining.Should().BeGreaterThan(0);
        detail.SalientIssues.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_UnknownCandidate_Throws()
    {
        await WithServiceAsync(async s =>
        {
            var act = () => s.CreateAsync("user-1", new CreateCivicCampaignRequest { CandidateSlug = "nope" });
            await act.Should().ThrowAsync<CivicCampaignValidationException>();
            return true;
        });
    }

    [Fact]
    public async Task Create_ClampsWeeksToConfiguredRange()
    {
        var detail = await WithServiceAsync(s => s.CreateAsync("user-weeks", new CreateCivicCampaignRequest
        {
            CandidateSlug = "sofia-alvarez",
            TotalWeeks = 999,
        }));
        detail.TotalWeeks.Should().BeLessThanOrEqualTo(16); // MaxTotalWeeks default
    }

    [Fact]
    public async Task TakeAction_DecrementsActionsAndRecordsAction()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-act", CreateReq()));
        var before = created.ActionsRemaining;

        var result = await WithServiceAsync(s => s.TakeActionAsync("user-act", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.PublishPost,
        }));

        result.ActionsRemaining.Should().Be(before - 1);
        result.Campaign.ThisWeekActions.Should().ContainSingle();
        // PublishPost should yield a templated post body (no LLM key in tests).
        result.GeneratedPostBody.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TakeAction_WhenNoActionsRemain_Throws()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-empty", CreateReq()));

        // Drain the week's action budget.
        for (var i = 0; i < created.ActionsRemaining; i++)
        {
            await WithServiceAsync(s => s.TakeActionAsync("user-empty", created.Id, new TakeActionRequest
            {
                ActionType = CivicCampaignActionType.TargetIssue,
            }));
        }

        await WithServiceAsync(async s =>
        {
            var act = () => s.TakeActionAsync("user-empty", created.Id, new TakeActionRequest
            {
                ActionType = CivicCampaignActionType.TargetIssue,
            });
            await act.Should().ThrowAsync<CivicCampaignConflictException>();
            return true;
        });
    }

    [Fact]
    public async Task AdvanceWeek_PersistsWeekAndAdvancesCounter()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-adv", CreateReq(weeks: 4)));

        var result = await WithServiceAsync(s => s.AdvanceWeekAsync("user-adv", created.Id));

        result.CompletedWeek.Should().Be(1);
        result.CampaignCompleted.Should().BeFalse();
        result.Campaign.CurrentWeek.Should().Be(2);
        result.Campaign.History.Should().ContainSingle(w => w.WeekNumber == 1);
        result.Standings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
    }

    [Fact]
    public async Task StrongOnBrandActions_OutperformNoActions()
    {
        // Manager A plays strong on-brand actions each week; Manager B passively advances.
        async Task<double> RunAsync(string user, bool act)
        {
            var created = await WithServiceAsync(s => s.CreateAsync(user, CreateReq(weeks: 4, diff: CivicCampaignDifficulty.Easy)));
            CivicCampaignDetailDto? detail = created;
            while (detail!.Status == "Active")
            {
                if (act)
                {
                    var issue = detail.SalientIssues.FirstOrDefault();
                    for (var i = 0; i < detail.ActionsRemaining; i++)
                    {
                        await WithServiceAsync(s => s.TakeActionAsync(user, created.Id, new TakeActionRequest
                        {
                            ActionType = CivicCampaignActionType.TargetIssue,
                            Target = issue,
                        }));
                    }
                }
                var adv = await WithServiceAsync(s => s.AdvanceWeekAsync(user, created.Id));
                detail = adv.Campaign;
            }
            var results = await WithServiceAsync(s => s.GetResultsAsync(user, created.Id));
            return results.FinalSupport;
        }

        var active = await RunAsync("mgr-active", act: true);
        var passive = await RunAsync("mgr-passive", act: false);

        active.Should().BeGreaterThan(passive);
    }

    [Fact]
    public async Task FullRun_CompletesWithWinnerDecidedAndResultsAvailable()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-full", CreateReq(weeks: 4, diff: CivicCampaignDifficulty.Easy)));

        AdvanceWeekResult? last = null;
        for (var i = 0; i < 4; i++)
        {
            // Take one strong action then advance.
            await WithServiceAsync(s => s.TakeActionAsync("user-full", created.Id, new TakeActionRequest
            {
                ActionType = CivicCampaignActionType.TargetIssue,
            }));
            last = await WithServiceAsync(s => s.AdvanceWeekAsync("user-full", created.Id));
        }

        last.Should().NotBeNull();
        last!.CampaignCompleted.Should().BeTrue();
        last.Campaign.Status.Should().Be("Completed");

        var results = await WithServiceAsync(s => s.GetResultsAsync("user-full", created.Id));
        results.Won.Should().Be(last.Campaign.Won!.Value);
        results.FinalStandings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
        results.SupportTrend.Should().HaveCount(4);
        results.FinalRank.Should().BeInRange(1, results.FieldSize);
    }

    [Fact]
    public async Task Ownership_OtherUserCannotSeeOrAdvanceCampaign()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("owner", CreateReq()));

        await WithServiceAsync(async s =>
        {
            var get = () => s.GetDetailAsync("intruder", created.Id);
            await get.Should().ThrowAsync<CivicCampaignNotFoundException>();

            var adv = () => s.AdvanceWeekAsync("intruder", created.Id);
            await adv.Should().ThrowAsync<CivicCampaignNotFoundException>();
            return true;
        });
    }

    [Fact]
    public async Task GetResults_BeforeCompletion_Throws()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-early", CreateReq()));
        await WithServiceAsync(async s =>
        {
            var act = () => s.GetResultsAsync("user-early", created.Id);
            await act.Should().ThrowAsync<CivicCampaignValidationException>();
            return true;
        });
    }

    [Fact]
    public async Task List_ReturnsOnlyCallersCampaigns()
    {
        await WithServiceAsync(s => s.CreateAsync("list-user-a", CreateReq()));
        await WithServiceAsync(s => s.CreateAsync("list-user-a", CreateReq()));
        await WithServiceAsync(s => s.CreateAsync("list-user-b", CreateReq()));

        var aList = await WithServiceAsync(s => s.ListAsync("list-user-a"));
        var bList = await WithServiceAsync(s => s.ListAsync("list-user-b"));

        aList.Should().HaveCount(2);
        bList.Should().HaveCount(1);
    }
}
