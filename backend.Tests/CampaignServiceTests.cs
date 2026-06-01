using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Arena.API.Tests;

public class CampaignServiceTests
{
    private static CreateCampaignRequest CreateReq(int? weeks = 4, string persona = "reformer") => new()
    {
        CandidateName = "Test Candidate",
        PersonaId = persona,
        Difficulty = CampaignDifficulty.Normal,
        TotalWeeks = weeks,
    };

    // ---------------------------------------------------------------- Create

    [Fact]
    public async Task CreateAsync_SetsStartingResourcesAndApproval()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();
        var t = h.Tuning;

        var detail = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));

        detail.Campaign.CurrentWeek.Should().Be(1);
        detail.Campaign.TotalWeeks.Should().Be(4);
        detail.Campaign.Status.Should().Be(nameof(CampaignStatus.Active));
        detail.CurrentApproval.Should().Be(t.StartingApproval);
        detail.Resources.Budget.Should().Be(t.StartingBudget);
        detail.Resources.TimeUnits.Should().Be(t.StartingTimeUnits);
        detail.Resources.StaffCount.Should().Be(t.StartingStaff);
        detail.Resources.Momentum.Should().Be(t.StartingMomentum);

        // Event generation is probabilistic (0-2 events), so the collection may legitimately be
        // empty (FluentAssertions' OnlyContain fails on empty, so assert per-item with All()).
        detail.PendingEvents.All(e => e.WeekNumber == 1 && !e.Resolved).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_AlwaysGeneratesWeekOneEvents_WhenChanceIsOne()
    {
        // Forcing the per-week event chance to 1.0 makes generation deterministic so we can assert
        // events ARE produced, all for week 1 and unresolved.
        using var h = new CampaignTestHarness(new CampaignTuningOptions { EventChancePerWeek = 1.0 });
        var user = h.SeedUser();

        var detail = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));

        detail.PendingEvents.Should().NotBeEmpty();
        detail.PendingEvents.Should().OnlyContain(e => e.WeekNumber == 1 && !e.Resolved);
    }

    [Fact]
    public async Task CreateAsync_UnknownPersona_Throws()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();

        var act = () => h.Run((svc, _) => svc.CreateAsync(user, CreateReq(persona: "does-not-exist")));
        await act.Should().ThrowAsync<CampaignValidationException>();
    }

    [Fact]
    public async Task CreateAsync_ClampsWeeksToConfiguredRange()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();
        var t = h.Tuning;

        var tooSmall = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq(weeks: 1)));
        tooSmall.Campaign.TotalWeeks.Should().Be(t.MinTotalWeeks);

        var tooBig = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq(weeks: 999)));
        tooBig.Campaign.TotalWeeks.Should().Be(t.MaxTotalWeeks);
    }

    // ---------------------------------------------------------------- Advance week

    [Fact]
    public async Task AdvanceWeekAsync_DeductsResources_AndProducesSnapshot()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();
        var t = h.Tuning;

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        var activities = new List<ActivityAllocationDto>
        {
            new() { Type = CampaignActivityType.Advertising, Budget = 10000 },
        };

        var result = await h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, activities));

        result.Detail.Campaign.CurrentWeek.Should().Be(2); // advanced from 1 -> 2
        result.Detail.Resources.Budget.Should().Be(t.StartingBudget - 10000);
        result.Detail.Resources.TimeUnits.Should().Be(t.StartingTimeUnits); // refilled for next week
        result.WeekSummary.WeekNumber.Should().Be(1);
        result.Detail.Weeks.Should().ContainSingle(w => w.WeekNumber == 1);
        result.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task AdvanceWeekAsync_OverAllocatingBudget_IsRejected()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        var activities = new List<ActivityAllocationDto>
        {
            new() { Type = CampaignActivityType.Advertising, Budget = 10_000_000 },
        };

        var act = () => h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, activities));
        await act.Should().ThrowAsync<CampaignValidationException>();
    }

    [Fact]
    public async Task AdvanceWeekAsync_OverAllocatingTime_IsRejected()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        // Each town hall costs 5 time units; 40 starting -> 100 town halls is well over budget.
        var activities = new List<ActivityAllocationDto>
        {
            new() { Type = CampaignActivityType.TownHall, Count = 100 },
        };

        var act = () => h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, activities));
        await act.Should().ThrowAsync<CampaignValidationException>();
    }

    // ---------------------------------------------------------------- Resolve event

    [Fact]
    public async Task ResolveEventAsync_AppliesOptionEffects_AndMarksResolved()
    {
        // Disable random event generation so this test is fully deterministic, then seed exactly
        // one known week-1 event to resolve.
        using var h = new CampaignTestHarness(new CampaignTuningOptions { EventChancePerWeek = 0 });
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        var seededEventId = Guid.NewGuid();
        await h.Run(async (_, db) =>
        {
            db.CampaignEvents.Add(new CampaignEvent
            {
                Id = seededEventId,
                CampaignId = id,
                WeekNumber = 1,
                Type = CampaignEventType.Opportunity,
                EventKey = "endorsement",
                Title = "Endorsement",
                Description = "desc",
                OptionsJson = "[{\"id\":\"accept\",\"label\":\"Accept and hold a joint rally\"}]",
                Resolved = false,
            });
            await db.SaveChangesAsync();
        });

        var snapshot = await h.Query<Campaign>()
            .Include(c => c.Events).Include(c => c.Resources)
            .FirstAsync(c => c.Id == id);
        var pending = snapshot.Events.Single(e => !e.Resolved && e.WeekNumber == 1);
        pending.Id.Should().Be(seededEventId);
        var option = CampaignEventBank.Find(pending.EventKey)!.Options.First();
        var approvalBefore = snapshot.Approval;
        var budgetBefore = snapshot.Resources.Budget;
        var momentumBefore = snapshot.Resources.Momentum;

        var detail = await h.Run((svc, _) => svc.ResolveEventAsync(id, user.Id, pending.Id, option.Id));

        detail.PendingEvents.Should().NotContain(e => e.Id == pending.Id);

        var refreshed = await h.Query<Campaign>()
            .Include(c => c.Events).Include(c => c.Resources)
            .FirstAsync(c => c.Id == id);
        var resolved = refreshed.Events.First(e => e.Id == pending.Id);
        resolved.Resolved.Should().BeTrue();
        resolved.ResponseChosen.Should().Be(option.Id);

        refreshed.Approval.Should().Be(CampaignMechanics.ClampApproval(approvalBefore + option.Approval));
        refreshed.Resources.Budget.Should().Be(Math.Max(0, budgetBefore + option.Budget));
        refreshed.Resources.Momentum.Should().Be(CampaignMechanics.Clamp(momentumBefore + option.Momentum, 0, 100));
    }

    [Fact]
    public async Task ResolveEventAsync_UnknownOption_Throws()
    {
        using var h = new CampaignTestHarness(new CampaignTuningOptions { EventChancePerWeek = 0 });
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        var seededEventId = Guid.NewGuid();
        await h.Run(async (_, db) =>
        {
            db.CampaignEvents.Add(new CampaignEvent
            {
                Id = seededEventId, CampaignId = id, WeekNumber = 1,
                Type = CampaignEventType.Opportunity, EventKey = "endorsement",
                Title = "t", Description = "d", OptionsJson = "[]", Resolved = false,
            });
            await db.SaveChangesAsync();
        });

        var act = () => h.Run((svc, _) => svc.ResolveEventAsync(id, user.Id, seededEventId, "not-a-real-option"));
        await act.Should().ThrowAsync<CampaignValidationException>();
    }

    // ---------------------------------------------------------------- Debate milestone

    [Fact]
    public async Task RunDebateMilestoneAsync_CreatesDebateWithTurnsVotes_AndUsesTemplatesWithEmptyKey()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();
        var t = h.Tuning;

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        // Advance week 1 -> now at week 2 (a debate milestone).
        await h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, new List<ActivityAllocationDto>()));

        var approvalBefore = (await h.Query<Campaign>().FirstAsync(c => c.Id == id)).Approval;

        var result = await h.Run((svc, _) => svc.RunDebateMilestoneAsync(id, user.Id, skip: false, topic: null));

        result.Skipped.Should().BeFalse();
        result.DebateId.Should().NotBeNull();
        result.Won.Should().NotBeNull();

        var debate = await h.Query<Debate>()
            .Include(d => d.Turns).Include(d => d.Votes)
            .FirstAsync(d => d.Id == result.DebateId!.Value);

        debate.CampaignId.Should().Be(id);
        debate.CampaignWeek.Should().Be(2);
        debate.Status.Should().Be(DebateStatus.Completed);
        debate.Turns.Should().HaveCount(t.TurnsPerDebate);
        debate.Votes.Should().NotBeEmpty();

        // Empty API key -> no LLM network call, templated turns used.
        h.Llm.TurnCalls.Should().Be(0);
        debate.Turns.Should().NotContain(turn => turn.Content == h.Llm.FixedContent);

        // Approval moved by the performance effect.
        var after = (await h.Query<Campaign>().FirstAsync(c => c.Id == id)).Approval;
        after.Should().NotBe(approvalBefore);
    }

    [Fact]
    public async Task RunDebateMilestoneAsync_Skip_AppliesPenalty()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();
        var t = h.Tuning;

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        await h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, new List<ActivityAllocationDto>()));

        var approvalBefore = (await h.Query<Campaign>().FirstAsync(c => c.Id == id)).Approval;

        var result = await h.Run((svc, _) => svc.RunDebateMilestoneAsync(id, user.Id, skip: true, topic: null));

        result.Skipped.Should().BeTrue();
        result.DebateId.Should().BeNull();
        result.Won.Should().BeNull();

        var after = (await h.Query<Campaign>().FirstAsync(c => c.Id == id)).Approval;
        after.Should().Be(CampaignMechanics.ClampApproval(approvalBefore - t.DebateSkipPenalty));
    }

    [Fact]
    public async Task AdvanceWeekAsync_BlockedWhenDebateMilestoneUnresolved()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq()));
        var id = created.Campaign.Id;

        // Advance to week 2 (milestone) but do not run the debate.
        await h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, new List<ActivityAllocationDto>()));

        var act = () => h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, new List<ActivityAllocationDto>()));
        await act.Should().ThrowAsync<CampaignConflictException>();
    }

    // ---------------------------------------------------------------- Full run

    [Fact]
    public async Task FullRun_FourWeeks_ReachesCompletedWithResults()
    {
        using var h = new CampaignTestHarness();
        var user = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(user, CreateReq(weeks: 4)));
        var id = created.Campaign.Id;

        var completed = false;
        var guard = 0;
        while (!completed && guard++ < 20)
        {
            var detail = await h.Run((svc, _) => svc.GetDetailAsync(id, user.Id));
            if (detail.DebateMilestoneDue)
                await h.Run((svc, _) => svc.RunDebateMilestoneAsync(id, user.Id, skip: false, topic: null));

            var advance = await h.Run((svc, _) => svc.AdvanceWeekAsync(id, user.Id, new List<ActivityAllocationDto>()));
            completed = advance.Completed;
        }

        completed.Should().BeTrue("a 4-week campaign should finalize");

        var final = await h.Query<Campaign>().FirstAsync(c => c.Id == id);
        final.Status.Should().Be(CampaignStatus.Completed);
        final.Won.Should().NotBeNull();
        final.FinalApproval.Should().NotBeNull();

        var results = await h.Run((svc, _) => svc.GetResultsAsync(id, user.Id));
        results.TotalWeeks.Should().Be(4);
        results.DebatesPlayed.Should().Be(2); // milestones at weeks 2 and 4
        results.DebatesWon.Should().BeInRange(0, 2);
        results.ApprovalTrend.Should().HaveCount(4);
    }

    // ---------------------------------------------------------------- Ownership

    [Fact]
    public async Task GetDetailAsync_WrongUser_ThrowsNotFound()
    {
        using var h = new CampaignTestHarness();
        var owner = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(owner, CreateReq()));
        var otherUserId = Guid.NewGuid();

        var act = () => h.Run((svc, _) => svc.GetDetailAsync(created.Campaign.Id, otherUserId));
        await act.Should().ThrowAsync<CampaignNotFoundException>();
    }

    [Fact]
    public async Task AdvanceWeekAsync_WrongUser_ThrowsNotFound()
    {
        using var h = new CampaignTestHarness();
        var owner = h.SeedUser();

        var created = await h.Run((svc, _) => svc.CreateAsync(owner, CreateReq()));
        var otherUserId = Guid.NewGuid();

        var act = () => h.Run((svc, _) =>
            svc.AdvanceWeekAsync(created.Campaign.Id, otherUserId, new List<ActivityAllocationDto>()));
        await act.Should().ThrowAsync<CampaignNotFoundException>();
    }
}
