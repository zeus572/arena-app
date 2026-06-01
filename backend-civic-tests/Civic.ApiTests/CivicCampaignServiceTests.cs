using System.Net.Http.Json;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Campaign;
using Civic.ApiTests.Fakes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Integration tests for the Campaign Manager game mode against the real civic_test DB and the
/// seeded President race + seeded briefings. The service is constructed directly with a real scoped
/// DbContext and a StubLlmClient with NO registered responses, so every LLM path falls back to its
/// deterministic templated branch — no network is hit.
/// </summary>
[Collection("Database")]
public class CivicCampaignServiceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public CivicCampaignServiceTests(DatabaseFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetMutableAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Cap days so full-run tests stay fast regardless of the real election date distance.
    private async Task<T> WithServiceAsync<T>(Func<CivicCampaignService, Task<T>> body, int maxDays = 6)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var postGen = scope.ServiceProvider.GetRequiredService<CampaignPostGenerationService>();
        var catalog = scope.ServiceProvider.GetRequiredService<Civic.API.Services.ICivicCatalog>();
        var svc = new CivicCampaignService(
            db,
            postGen,
            new StubLlmClient(), // no responses registered → forces templated fallback
            catalog,
            Options.Create(new CivicCampaignOptions { OpponentVariance = 0, MaxCampaignDays = maxDays }),
            NullLogger<CivicCampaignService>.Instance);
        return await body(svc);
    }

    private static CreateCivicCampaignRequest CreateReq(CivicCampaignDifficulty diff = CivicCampaignDifficulty.Normal) => new()
    {
        CandidateSlug = "sofia-alvarez",
        Difficulty = diff,
    };

    private async Task<string> FirstNewsSlugAsync(CivicCampaignDetailDto detail)
    {
        detail.NewsItems.Should().NotBeEmpty("seeded briefings should be offered as news items");
        return await Task.FromResult(detail.NewsItems[0].BriefingSlug);
    }

    [Fact]
    public async Task GetRaces_ReturnsSeededPresidentRaceWithMultipleCandidates()
    {
        var races = await WithServiceAsync(s => s.GetRacesAsync());
        var president = races.Should().ContainSingle(r => r.Office == "President").Subject;
        president.Candidates.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Create_SnapsToUpcomingElection_AndComputesDays()
    {
        var detail = await WithServiceAsync(s => s.CreateAsync("user-1", CreateReq()));

        detail.ElectionName.Should().NotBeNullOrWhiteSpace();
        detail.ElectionDate.Should().BeAfter(DateTime.UtcNow);
        detail.TotalDays.Should().BeGreaterThan(0);
        detail.DaysRemaining.Should().BeGreaterThan(0);
        detail.CurrentDay.Should().Be(1);
        detail.ActionsRemaining.Should().Be(2); // ActionsPerDay default
        detail.Standings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
        detail.Standings.Should().ContainSingle(s => s.IsPlayer);
    }

    [Fact]
    public async Task Detail_OffersNewsItemsWithResponseOptions()
    {
        var detail = await WithServiceAsync(s => s.CreateAsync("user-news", CreateReq()));

        detail.NewsItems.Should().NotBeEmpty();
        detail.NewsItems.Count.Should().BeLessThanOrEqualTo(6); // NewsItemsToOffer
        foreach (var item in detail.NewsItems)
        {
            item.BriefingSlug.Should().NotBeNullOrWhiteSpace();
            item.Options.Should().HaveCountGreaterThanOrEqualTo(2);
            item.Options.Should().OnlyContain(o => !string.IsNullOrWhiteSpace(o.Id) && !string.IsNullOrWhiteSpace(o.Label));
        }
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
    public async Task RespondToNews_SpendsActionPublishesPostAndRemovesItem()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-resp", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);
        var optionId = created.NewsItems[0].Options[0].Id;
        var before = created.ActionsRemaining;

        var result = await WithServiceAsync(s => s.TakeActionAsync("user-resp", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.RespondToNews,
            BriefingSlug = slug,
            OptionId = optionId,
        }));

        result.ActionsRemaining.Should().Be(before - 1);
        result.GeneratedPostBody.Should().NotBeNullOrWhiteSpace();
        result.Campaign.TodayActions.Should().ContainSingle(a => a.ActionType == "RespondToNews");
        // The responded item should no longer be offered.
        result.Campaign.NewsItems.Should().NotContain(n => n.BriefingSlug == slug);
    }

    [Fact]
    public async Task RespondToNews_SameItemTwice_Throws()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-dup", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);
        var optionId = created.NewsItems[0].Options[0].Id;

        await WithServiceAsync(s => s.TakeActionAsync("user-dup", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.RespondToNews,
            BriefingSlug = slug,
            OptionId = optionId,
        }));

        await WithServiceAsync(async s =>
        {
            var act = () => s.TakeActionAsync("user-dup", created.Id, new TakeActionRequest
            {
                ActionType = CivicCampaignActionType.RespondToNews,
                BriefingSlug = slug,
                OptionId = optionId,
            });
            await act.Should().ThrowAsync<CivicCampaignConflictException>();
            return true;
        });
    }

    [Fact]
    public async Task RespondToNews_UnknownBriefing_Throws()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-badnews", CreateReq()));
        await WithServiceAsync(async s =>
        {
            var act = () => s.TakeActionAsync("user-badnews", created.Id, new TakeActionRequest
            {
                ActionType = CivicCampaignActionType.RespondToNews,
                BriefingSlug = "does-not-exist",
                OptionId = "opt1",
            });
            await act.Should().ThrowAsync<CivicCampaignValidationException>();
            return true;
        });
    }

    [Fact]
    public async Task TakeAction_WhenNoActionsRemain_Throws()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-empty", CreateReq()));

        // Drain the day's action budget with secondary actions.
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
    public async Task AdvanceDay_PersistsDayAndAdvancesCounter()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-adv", CreateReq()));

        var result = await WithServiceAsync(s => s.AdvanceDayAsync("user-adv", created.Id));

        result.CompletedDay.Should().Be(1);
        result.Campaign.CurrentDay.Should().Be(2);
        result.Campaign.ActionsRemaining.Should().Be(2);
        result.Campaign.History.Should().ContainSingle(w => w.DayNumber == 1);
        result.Standings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
    }

    [Fact]
    public async Task RespondingToNews_OutperformsPassivePlay()
    {
        async Task<double> RunAsync(string user, bool act)
        {
            var created = await WithServiceAsync(s => s.CreateAsync(user, CreateReq(CivicCampaignDifficulty.Easy)));
            CivicCampaignDetailDto detail = created;
            var safety = 0;
            while (detail.Status == "Active" && safety++ < 400)
            {
                if (act)
                {
                    foreach (var item in detail.NewsItems.Take(detail.ActionsRemaining))
                    {
                        var r = await WithServiceAsync(s => s.TakeActionAsync(user, created.Id, new TakeActionRequest
                        {
                            ActionType = CivicCampaignActionType.RespondToNews,
                            BriefingSlug = item.BriefingSlug,
                            OptionId = item.Options[0].Id,
                        }));
                        detail = r.Campaign;
                    }
                }
                var adv = await WithServiceAsync(s => s.AdvanceDayAsync(user, created.Id));
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
        var created = await WithServiceAsync(s => s.CreateAsync("user-full", CreateReq(CivicCampaignDifficulty.Easy)));

        AdvanceDayResult? last = null;
        var safety = 0;
        while (safety++ < 400)
        {
            last = await WithServiceAsync(s => s.AdvanceDayAsync("user-full", created.Id));
            if (last.CampaignCompleted) break;
        }

        last.Should().NotBeNull();
        last!.CampaignCompleted.Should().BeTrue();
        last.Campaign.Status.Should().Be("Completed");

        var results = await WithServiceAsync(s => s.GetResultsAsync("user-full", created.Id));
        results.Won.Should().Be(last.Campaign.Won!.Value);
        results.FinalStandings.Sum(s => s.SupportShare).Should().BeApproximately(100.0, 0.5);
        results.SupportTrend.Should().NotBeEmpty();
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

            var adv = () => s.AdvanceDayAsync("intruder", created.Id);
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
    public async Task NewsResponsePage_ReturnsProfileValuesAndOptionBodies()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-page", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);

        var page = await WithServiceAsync(s => s.GetNewsResponsePageAsync("user-page", created.Id, slug));

        page.CandidateName.Should().NotBeNullOrWhiteSpace();
        page.CandidateBio.Should().NotBeNullOrWhiteSpace();
        page.Values.Should().NotBeEmpty("the response page shows the candidate's value axes");
        page.Headline.Should().NotBeNullOrWhiteSpace();
        page.Options.Should().HaveCountGreaterThanOrEqualTo(2);
        page.Options.Should().OnlyContain(o => !string.IsNullOrWhiteSpace(o.Body), "each option shows its full post text");
        page.AlreadyResponded.Should().BeFalse();
    }

    [Fact]
    public async Task NewsResponseOptions_AreLongerThanATweet()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-long", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);

        var page = await WithServiceAsync(s => s.GetNewsResponsePageAsync("user-long", created.Id, slug));

        // Responses are multi-sentence now — at least one option exceeds the old 160-char tweet cap,
        // and all stay within the configured max.
        page.Options.Should().Contain(o => o.Body.Length > 160);
        page.Options.Should().OnlyContain(o => o.Body.Length <= 600);
    }

    [Fact]
    public async Task NewsResponsePage_AfterResponding_FlagsAlreadyResponded()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("user-page2", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);
        var optionId = created.NewsItems[0].Options[0].Id;

        await WithServiceAsync(s => s.TakeActionAsync("user-page2", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.RespondToNews,
            BriefingSlug = slug,
            OptionId = optionId,
        }));

        var page = await WithServiceAsync(s => s.GetNewsResponsePageAsync("user-page2", created.Id, slug));
        page.AlreadyResponded.Should().BeTrue();
    }

    [Fact]
    public async Task TailoredFeed_OwnerSeesResponse_OtherUserDoesNot()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("feed-owner", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);
        var optionId = created.NewsItems[0].Options[0].Id;
        var candidateSlug = created.CandidateSlug;

        var res = await WithServiceAsync(s => s.TakeActionAsync("feed-owner", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.RespondToNews,
            BriefingSlug = slug,
            OptionId = optionId,
        }));
        var postId = res.Action.GeneratedPostId!.Value.ToString();

        // The owner sees their published response in the candidate feed...
        var ownerClient = _fx.Factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-User-Id", "feed-owner");
        var ownerFeed = await ownerClient.GetFromJsonAsync<CampaignFeedDto>($"/api/candidates/{candidateSlug}/posts?limit=100");
        ownerFeed!.Items.Should().Contain(p => p.Id.ToString() == postId);

        // ...a different user does not.
        var otherClient = _fx.Factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-User-Id", "feed-stranger");
        var otherFeed = await otherClient.GetFromJsonAsync<CampaignFeedDto>($"/api/candidates/{candidateSlug}/posts?limit=100");
        otherFeed!.Items.Should().NotContain(p => p.Id.ToString() == postId);
    }

    [Fact]
    public async Task PublishedResponse_IsAttributedToTheOwningUser()
    {
        var created = await WithServiceAsync(s => s.CreateAsync("owner-feed", CreateReq()));
        var slug = await FirstNewsSlugAsync(created);
        var optionId = created.NewsItems[0].Options[0].Id;

        var res = await WithServiceAsync(s => s.TakeActionAsync("owner-feed", created.Id, new TakeActionRequest
        {
            ActionType = CivicCampaignActionType.RespondToNews,
            BriefingSlug = slug,
            OptionId = optionId,
        }));

        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
        var post = await db.CampaignPosts.FirstAsync(p => p.Id == res.Action.GeneratedPostId);
        post.OwnerUserId.Should().Be("owner-feed");
        post.CampaignId.Should().Be(created.Id);
        post.TriggerBriefingSlug.Should().Be(slug);
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
