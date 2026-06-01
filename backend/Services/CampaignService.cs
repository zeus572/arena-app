using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;

namespace Arena.API.Services;

/// <summary>
/// Scoped orchestrator for the Campaign Manager game mode. Owns all DB persistence and
/// drives the pure <see cref="CampaignMechanics"/> formulas. LLM use is guarded — it only
/// hits the network when an Anthropic API key is configured, and always has a templated fallback.
/// </summary>
public class CampaignService
{
    private readonly ArenaDbContext _db;
    private readonly ILlmService _llm;
    private readonly IConfiguration _config;
    private readonly CampaignTuningOptions _t;
    private readonly ILogger<CampaignService> _logger;
    private readonly Random _random = new();

    public CampaignService(
        ArenaDbContext db,
        ILlmService llm,
        IConfiguration config,
        IOptions<CampaignTuningOptions> options,
        ILogger<CampaignService> logger)
    {
        _db = db;
        _llm = llm;
        _config = config;
        _t = options.Value;
        _logger = logger;
    }

    private bool LlmEnabled => !string.IsNullOrWhiteSpace(_config["Anthropic:ApiKey"]);

    // ---------------------------------------------------------------- Create

    public async Task<CampaignDetailDto> CreateAsync(User user, CreateCampaignRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CandidateName))
            throw new CampaignValidationException("Candidate name is required.");

        var persona = PersonaBank.Get(req.PersonaId)
            ?? throw new CampaignValidationException($"Unknown persona '{req.PersonaId}'.");

        var totalWeeks = req.TotalWeeks ?? _t.DefaultTotalWeeks;
        totalWeeks = (int)CampaignMechanics.Clamp(totalWeeks, _t.MinTotalWeeks, _t.MaxTotalWeeks);

        var theme = string.IsNullOrWhiteSpace(req.Theme) ? persona.Theme : req.Theme!.Trim();
        var platformJson = req.Platform is { Count: > 0 }
            ? JsonSerializer.Serialize(req.Platform)
            : "{}";

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CandidateName = req.CandidateName.Trim(),
            PersonaId = persona.Key,
            Persona = persona.Persona,
            OpponentName = persona.OpponentName,
            OpponentPersona = persona.OpponentPersona,
            Theme = theme,
            PlatformJson = platformJson,
            CurrentWeek = 1,
            TotalWeeks = totalWeeks,
            Difficulty = req.Difficulty,
            Status = CampaignStatus.Active,
            Approval = _t.StartingApproval,
            LastResolvedDebateWeek = 0,
        };
        campaign.Resources = new CampaignResources
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Budget = _t.StartingBudget,
            TimeUnits = _t.StartingTimeUnits,
            StaffCount = _t.StartingStaff,
            Momentum = _t.StartingMomentum,
        };

        _db.Campaigns.Add(campaign);

        GenerateEventsForWeek(campaign, 1);

        await _db.SaveChangesAsync();

        return await GetDetailAsync(campaign.Id, user.Id);
    }

    // ---------------------------------------------------------------- List / Detail

    public async Task<List<CampaignSummaryDto>> ListAsync(Guid userId)
    {
        var campaigns = await _db.Campaigns
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
        return campaigns.Select(ToSummary).ToList();
    }

    public async Task<CampaignDetailDto> GetDetailAsync(Guid id, Guid userId)
    {
        var campaign = await LoadOwnedAsync(id, userId);
        return ToDetail(campaign);
    }

    // ---------------------------------------------------------------- Preview allocation

    public async Task<AllocationPreviewResult> PreviewAllocationAsync(
        Guid id, Guid userId, List<ActivityAllocationDto> activities)
    {
        var campaign = await LoadOwnedAsync(id, userId);
        var res = campaign.Resources;

        var (budgetCost, timeCost, _, _, lineItems, issues) =
            ComputeCosts(activities, res.StaffCount);

        var projectedBudget = res.Budget - budgetCost;
        var projectedTime = res.TimeUnits - timeCost;

        if (projectedBudget < 0) issues.Add("Not enough budget for these activities.");
        if (projectedTime < 0) issues.Add("Not enough time units for these activities.");

        return new AllocationPreviewResult
        {
            Affordable = issues.Count == 0,
            ProjectedBudget = projectedBudget,
            ProjectedTimeUnits = projectedTime,
            ProjectedStaff = res.StaffCount,
            Issues = issues,
            LineItems = lineItems.Cast<object>().ToList(),
        };
    }

    // ---------------------------------------------------------------- Advance week

    public async Task<AdvanceWeekResult> AdvanceWeekAsync(
        Guid id, Guid userId, List<ActivityAllocationDto> activities)
    {
        var campaign = await LoadOwnedAsync(id, userId);

        if (campaign.Status != CampaignStatus.Active)
            throw new CampaignValidationException("Campaign is not active.");

        if (IsDebateMilestone(campaign.CurrentWeek)
            && campaign.LastResolvedDebateWeek < campaign.CurrentWeek)
        {
            throw new CampaignConflictException(
                "This week's debate milestone must be resolved before advancing.");
        }

        var res = campaign.Resources;
        var (budgetCost, timeCost, fundraisingGain, momentumGain, _, issues) =
            ComputeCosts(activities, res.StaffCount);

        if (res.Budget - budgetCost < 0) issues.Add("Not enough budget.");
        if (res.TimeUnits - timeCost < 0) issues.Add("Not enough time units.");
        if (issues.Count > 0)
            throw new CampaignValidationException(string.Join(" ", issues));

        // Apply resource costs and gains.
        res.Budget = res.Budget - budgetCost + fundraisingGain;
        res.TimeUnits -= timeCost;

        var advertisingSpend = activities
            .Where(a => a.Type == CampaignActivityType.Advertising)
            .Sum(a => a.Budget ?? 0);
        var townHallCount = activities
            .Where(a => a.Type == CampaignActivityType.TownHall)
            .Sum(a => a.Count ?? 1);

        // Resolved-event approval effects for the current week.
        var eventApprovalEffect = campaign.Events
            .Where(e => e.WeekNumber == campaign.CurrentWeek && e.Resolved)
            .Sum(e => ReadAppliedApproval(e));

        var input = new WeekInput
        {
            PrevApproval = campaign.Approval,
            Momentum = res.Momentum,
            Difficulty = campaign.Difficulty,
            Week = campaign.CurrentWeek,
            AdvertisingSpend = advertisingSpend,
            TownHallCount = townHallCount,
            EventApprovalEffect = eventApprovalEffect,
            DebateApprovalEffect = 0, // already folded into Approval when the debate resolved
            ExtraMomentumGain = momentumGain,
        };

        var result = CampaignMechanics.ComputeWeek(input, _t);

        var now = DateTime.UtcNow;
        var resourceChanges = new
        {
            budgetCost,
            fundraisingGain,
            timeCost,
            momentumGain,
            newBudget = res.Budget,
            newTimeUnits = res.TimeUnits,
        };

        // Link any debate run this week onto the snapshot.
        var debateThisWeek = await _db.Debates
            .Where(d => d.CampaignId == campaign.Id && d.CampaignWeek == campaign.CurrentWeek)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();

        var snapshot = new CampaignWeek
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            WeekNumber = campaign.CurrentWeek,
            ApprovalRating = result.NewApproval,
            DecisionsJson = JsonSerializer.Serialize(activities),
            ResourceChangesJson = JsonSerializer.Serialize(resourceChanges),
            DebateId = debateThisWeek,
            Summary = BuildWeekSummary(campaign.CurrentWeek, result),
            CreatedAt = now,
        };
        _db.CampaignWeeks.Add(snapshot);

        campaign.Approval = result.NewApproval;
        res.Momentum = result.NewMomentum;
        res.TimeUnits = _t.StartingTimeUnits; // refill for next week
        res.UpdatedAt = now;
        campaign.UpdatedAt = now;

        var completed = false;
        if (campaign.CurrentWeek >= campaign.TotalWeeks)
        {
            FinalizeCampaign(campaign);
            completed = true;
        }
        else
        {
            campaign.CurrentWeek++;
            GenerateEventsForWeek(campaign, campaign.CurrentWeek);
        }

        await _db.SaveChangesAsync();

        var detail = ToDetail(campaign);
        return new AdvanceWeekResult
        {
            Detail = detail,
            WeekSummary = ToWeekDto(snapshot),
            Completed = completed,
            DebateMilestoneDue = detail.DebateMilestoneDue,
        };
    }

    // ---------------------------------------------------------------- Resolve event

    public async Task<CampaignDetailDto> ResolveEventAsync(
        Guid id, Guid userId, Guid eventId, string optionId)
    {
        var campaign = await LoadOwnedAsync(id, userId);

        var ev = campaign.Events.FirstOrDefault(e => e.Id == eventId)
            ?? throw new CampaignNotFoundException("Event not found.");

        if (ev.Resolved)
            throw new CampaignValidationException("Event already resolved.");
        if (ev.WeekNumber != campaign.CurrentWeek)
            throw new CampaignValidationException("Event does not belong to the current week.");

        var option = CampaignEventBank.FindOption(ev.EventKey, optionId)
            ?? throw new CampaignValidationException($"Unknown option '{optionId}' for this event.");

        var res = campaign.Resources;
        res.Budget = Math.Max(0, res.Budget + option.Budget);
        res.Momentum = CampaignMechanics.Clamp(res.Momentum + option.Momentum, 0, 100);
        campaign.Approval = CampaignMechanics.ClampApproval(campaign.Approval + option.Approval);

        var now = DateTime.UtcNow;
        ev.Resolved = true;
        ev.ResponseChosen = option.Id;
        ev.ResolvedAt = now;
        ev.OutcomeJson = JsonSerializer.Serialize(new
        {
            approval = option.Approval,
            budget = option.Budget,
            momentum = option.Momentum,
        });
        res.UpdatedAt = now;
        campaign.UpdatedAt = now;

        await _db.SaveChangesAsync();
        return ToDetail(campaign);
    }

    // ---------------------------------------------------------------- Debate milestone

    public async Task<DebateMilestoneResult> RunDebateMilestoneAsync(
        Guid id, Guid userId, bool skip, string? topic)
    {
        var campaign = await LoadOwnedAsync(id, userId);

        if (campaign.Status != CampaignStatus.Active)
            throw new CampaignValidationException("Campaign is not active.");
        if (!IsDebateMilestone(campaign.CurrentWeek))
            throw new CampaignValidationException("This week has no debate milestone.");
        if (campaign.LastResolvedDebateWeek >= campaign.CurrentWeek)
            throw new CampaignConflictException("This week's debate milestone is already resolved.");

        var now = DateTime.UtcNow;

        if (skip)
        {
            if (_t.DebatesMandatory)
                throw new CampaignValidationException("Debates are mandatory and cannot be skipped.");

            campaign.Approval = CampaignMechanics.ClampApproval(campaign.Approval - _t.DebateSkipPenalty);
            campaign.LastResolvedDebateWeek = campaign.CurrentWeek;
            campaign.UpdatedAt = now;
            await _db.SaveChangesAsync();

            return new DebateMilestoneResult
            {
                DebateId = null,
                Skipped = true,
                Won = null,
                SignedEffect = -_t.DebateSkipPenalty,
                Summary = $"Skipped the week {campaign.CurrentWeek} debate — a {_t.DebateSkipPenalty:F0}-point polling penalty.",
                Detail = ToDetail(campaign),
            };
        }

        var (candidate, opponent) = await EnsureAgentsAsync(campaign);

        var topicText = string.IsNullOrWhiteSpace(topic)
            ? $"The future of {campaign.Theme}"
            : topic!.Trim();

        var debate = new Debate
        {
            Id = Guid.NewGuid(),
            Topic = topicText,
            Status = DebateStatus.Active,
            ProponentId = candidate.Id,
            OpponentId = opponent.Id,
            Format = "standard",
            Source = "bot",
            CampaignId = campaign.Id,
            CampaignWeek = campaign.CurrentWeek,
        };
        _db.Debates.Add(debate);
        await _db.SaveChangesAsync();

        await GenerateTurnsAsync(debate, candidate, opponent);

        var variance = _random.NextDouble() * 20 - 10; // [-10, 10]
        var perf = CampaignMechanics.DebatePerformanceResult(
            campaign.Resources.Momentum, campaign.Difficulty, campaign.CurrentWeek, variance, _t);

        var approvalEffect = perf.Signed * _t.DebatePerformanceWeight;
        campaign.Approval = CampaignMechanics.ClampApproval(campaign.Approval + approvalEffect);

        PersistSimulatedVotes(debate, candidate, opponent, perf);

        debate.Status = DebateStatus.Completed;
        debate.UpdatedAt = now;
        campaign.LastResolvedDebateWeek = campaign.CurrentWeek;
        campaign.UpdatedAt = now;

        // Link the debate onto the most recent week snapshot if one exists.
        var lastSnapshot = campaign.Weeks
            .Where(w => w.WeekNumber == campaign.CurrentWeek)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefault();
        if (lastSnapshot is not null && lastSnapshot.DebateId is null)
            lastSnapshot.DebateId = debate.Id;

        await _db.SaveChangesAsync();

        var outcomeWord = perf.Won ? "won" : "lost";
        return new DebateMilestoneResult
        {
            DebateId = debate.Id,
            Skipped = false,
            Won = perf.Won,
            SignedEffect = approvalEffect,
            Summary = $"You {outcomeWord} the week {campaign.CurrentWeek} debate "
                      + $"({perf.PlayerScore:F0} vs {perf.OpponentScore:F0}), "
                      + $"a {approvalEffect:+0.0;-0.0} approval swing.",
            Detail = ToDetail(campaign),
        };
    }

    // ---------------------------------------------------------------- Results

    public async Task<CampaignResultsDto> GetResultsAsync(Guid id, Guid userId)
    {
        var campaign = await LoadOwnedAsync(id, userId);

        if (campaign.Status != CampaignStatus.Completed)
            throw new CampaignValidationException("Campaign is not yet completed.");

        var debates = await _db.Debates
            .Where(d => d.CampaignId == campaign.Id)
            .Include(d => d.Votes)
            .ToListAsync();

        var debatesWon = debates.Count(d =>
            d.Votes.Count(v => v.VotedForAgentId == d.ProponentId)
            > d.Votes.Count(v => v.VotedForAgentId == d.OpponentId));

        var trend = campaign.Weeks
            .OrderBy(w => w.WeekNumber)
            .Select(w => w.ApprovalRating)
            .ToList();

        return new CampaignResultsDto
        {
            CandidateName = campaign.CandidateName,
            Won = campaign.Won ?? false,
            FinalApproval = campaign.FinalApproval ?? campaign.Approval,
            TotalWeeks = campaign.TotalWeeks,
            DebatesPlayed = debates.Count,
            DebatesWon = debatesWon,
            ApprovalTrend = trend,
            Outcome = campaign.Outcome ?? string.Empty,
        };
    }

    // ---------------------------------------------------------------- Internals

    private async Task<Campaign> LoadOwnedAsync(Guid id, Guid userId)
    {
        var campaign = await _db.Campaigns
            .Include(c => c.Resources)
            .Include(c => c.Weeks)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null || campaign.UserId != userId)
            throw new CampaignNotFoundException();

        return campaign;
    }

    private void FinalizeCampaign(Campaign campaign)
    {
        var outcome = CampaignMechanics.ComputeOutcome(campaign.Approval, _t);
        campaign.Status = CampaignStatus.Completed;
        campaign.CompletedAt = DateTime.UtcNow;
        campaign.FinalApproval = outcome.FinalApproval;
        campaign.Won = outcome.Won;
        campaign.Outcome = outcome.Outcome;
    }

    private bool IsDebateMilestone(int week)
        => _t.DebateMilestoneEveryNWeeks > 0 && week % _t.DebateMilestoneEveryNWeeks == 0;

    private void GenerateEventsForWeek(Campaign campaign, int week)
    {
        // 0–2 events based on EventChancePerWeek (two independent rolls).
        var recentKeys = campaign.Events
            .OrderByDescending(e => e.CreatedAt)
            .Take(4)
            .Select(e => e.EventKey)
            .ToList();

        var usedThisWeek = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            if (_random.NextDouble() > _t.EventChancePerWeek) continue;

            var template = CampaignEventBank.Pick(_random, recentKeys.Concat(usedThisWeek));
            usedThisWeek.Add(template.EventKey);

            var optionsForClient = template.Options
                .Select(o => new { id = o.Id, label = o.Label })
                .ToList();

            var newEvent = new CampaignEvent
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                WeekNumber = week,
                Type = template.Type,
                EventKey = template.EventKey,
                Title = template.Title,
                Description = template.Description,
                OptionsJson = JsonSerializer.Serialize(optionsForClient),
                Resolved = false,
            };
            // Add through the DbSet (not just the navigation collection) so EF reliably tracks the
            // entity as Added. When the parent campaign is already tracked (e.g. during AdvanceWeek),
            // adding a child that carries a pre-assigned PK to the nav collection can be mis-detected
            // as Modified, which then emits an UPDATE for a non-existent row on SaveChanges.
            campaign.Events.Add(newEvent);
            _db.CampaignEvents.Add(newEvent);
        }
    }

    private (double budgetCost, int timeCost, double fundraisingGain, double momentumGain,
        List<AllocationLineItem> lineItems, List<string> issues)
        ComputeCosts(List<ActivityAllocationDto> activities, int staffCount)
    {
        double budgetCost = 0;
        int timeCost = 0;
        double fundraisingGain = 0;
        double momentumGain = 0;
        var lineItems = new List<AllocationLineItem>();
        var issues = new List<string>();

        foreach (var a in activities)
        {
            switch (a.Type)
            {
                case CampaignActivityType.Advertising:
                {
                    var spend = a.Budget ?? 0;
                    if (spend < 0) { issues.Add("Advertising spend cannot be negative."); break; }
                    budgetCost += spend;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.Advertising),
                        BudgetCost = spend, TimeCost = 0,
                        Note = $"~{CampaignMechanics.AdvertisingApproval(spend, 50, _t):F1} approval at neutral momentum",
                    });
                    break;
                }
                case CampaignActivityType.TownHall:
                {
                    var count = Math.Max(1, a.Count ?? 1);
                    var time = _t.TownHallTimeCost * count;
                    timeCost += time;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.TownHall),
                        BudgetCost = 0, TimeCost = time,
                        Note = $"{count} town hall(s)",
                    });
                    break;
                }
                case CampaignActivityType.Fundraising:
                {
                    timeCost += _t.FundraisingTimeCost;
                    var gain = CampaignMechanics.Fundraising(staffCount, _t);
                    fundraisingGain += gain;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.Fundraising),
                        BudgetCost = 0, TimeCost = _t.FundraisingTimeCost,
                        Note = $"+${gain:N0} from {staffCount} staff",
                    });
                    break;
                }
                case CampaignActivityType.OppResearch:
                {
                    timeCost += _t.OppResearchStaffTimeCost;
                    momentumGain += _t.OppResearchMomentum;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.OppResearch),
                        BudgetCost = 0, TimeCost = _t.OppResearchStaffTimeCost,
                        Note = $"+{_t.OppResearchMomentum:F0} momentum",
                    });
                    break;
                }
                case CampaignActivityType.DebatePrep:
                {
                    timeCost += _t.DebatePrepTimeCost;
                    momentumGain += _t.DebatePrepMomentum;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.DebatePrep),
                        BudgetCost = 0, TimeCost = _t.DebatePrepTimeCost,
                        Note = $"+{_t.DebatePrepMomentum:F0} momentum",
                    });
                    break;
                }
                case CampaignActivityType.Polling:
                {
                    budgetCost += _t.PollingBudgetCost;
                    lineItems.Add(new AllocationLineItem
                    {
                        Type = nameof(CampaignActivityType.Polling),
                        BudgetCost = _t.PollingBudgetCost, TimeCost = 0,
                        Note = "Polling insight",
                    });
                    break;
                }
            }
        }

        return (budgetCost, timeCost, fundraisingGain, momentumGain, lineItems, issues);
    }

    private async Task<(Agent candidate, Agent opponent)> EnsureAgentsAsync(Campaign campaign)
    {
        var candidateName = $"Candidate-{campaign.Id:N}";
        var opponentName = $"Opponent-{campaign.Id:N}";

        var candidate = await _db.Agents.FirstOrDefaultAsync(a => a.Name == candidateName);
        if (candidate is null)
        {
            candidate = new Agent
            {
                Id = Guid.NewGuid(),
                Name = candidateName,
                Description = $"{campaign.CandidateName} — {campaign.Theme}",
                Persona = campaign.Persona,
                ReputationScore = 50,
            };
            _db.Agents.Add(candidate);
        }

        var opponent = await _db.Agents.FirstOrDefaultAsync(a => a.Name == opponentName);
        if (opponent is null)
        {
            opponent = new Agent
            {
                Id = Guid.NewGuid(),
                Name = opponentName,
                Description = campaign.OpponentName,
                Persona = campaign.OpponentPersona,
                ReputationScore = 50,
            };
            _db.Agents.Add(opponent);
        }

        await _db.SaveChangesAsync();
        return (candidate, opponent);
    }

    private async Task GenerateTurnsAsync(Debate debate, Agent candidate, Agent opponent)
    {
        var turns = new List<Turn>();
        for (var i = 0; i < _t.TurnsPerDebate; i++)
        {
            var isCandidate = i % 2 == 0;
            var speaker = isCandidate ? candidate : opponent;
            var content = await GenerateTurnContentAsync(speaker, debate, turns, isCandidate ? opponent : candidate);

            var turn = new Turn
            {
                Id = Guid.NewGuid(),
                DebateId = debate.Id,
                AgentId = speaker.Id,
                TurnNumber = i + 1,
                Type = TurnType.Argument,
                Content = content,
            };
            turns.Add(turn);
            _db.Turns.Add(turn);
        }
        await _db.SaveChangesAsync();
    }

    private async Task<string> GenerateTurnContentAsync(
        Agent speaker, Debate debate, List<Turn> previousTurns, Agent other)
    {
        if (LlmEnabled)
        {
            try
            {
                var result = await _llm.GenerateTurnAsync(
                    speaker, debate, previousTurns, TurnType.Argument, opponent: other);
                if (!string.IsNullOrWhiteSpace(result.Content))
                    return result.Content;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "LLM turn generation failed for campaign debate {DebateId}; falling back to template.",
                    debate.Id);
            }
        }

        return TemplatedTurn(speaker, debate, previousTurns.Count + 1);
    }

    private static string TemplatedTurn(Agent speaker, Debate debate, int turnNumber)
        => $"As {speaker.Name}, I want to be clear about where I stand on \"{debate.Topic}.\" "
           + $"My record speaks for itself, and the choice in this race could not be sharper. "
           + $"(Turn {turnNumber})";

    private void PersistSimulatedVotes(Debate debate, Agent candidate, Agent opponent, DebatePerformance perf)
    {
        const int totalVotes = 20;
        // Logistic share for the candidate based on the score margin.
        var share = 1.0 / (1.0 + Math.Exp(-perf.Margin / 10.0));
        var candidateVotes = (int)Math.Round(totalVotes * share);
        candidateVotes = (int)CampaignMechanics.Clamp(candidateVotes, 0, totalVotes);
        var opponentVotes = totalVotes - candidateVotes;

        for (var i = 0; i < candidateVotes; i++)
            _db.Votes.Add(MakeVote(debate.Id, candidate.Id));
        for (var i = 0; i < opponentVotes; i++)
            _db.Votes.Add(MakeVote(debate.Id, opponent.Id));
    }

    private Vote MakeVote(Guid debateId, Guid agentId) => new()
    {
        Id = Guid.NewGuid(),
        DebateId = debateId,
        // Each simulated vote gets its OWN throwaway anonymous voter. Votes carry a unique index on
        // (DebateId, UserId), so reusing a single sim-voter across the ~20 votes would collide.
        UserId = NewSimVoterId(),
        VotedForAgentId = agentId,
    };

    // Simulated debate votes belong to throwaway anonymous users — one fresh user per vote.
    private Guid NewSimVoterId()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"simvoter-{Guid.NewGuid():N}"[..16],
            Email = $"{Guid.NewGuid():N}@arena.local",
            IsAnonymous = true,
        };
        _db.Users.Add(user);
        return user.Id;
    }

    private static double ReadAppliedApproval(CampaignEvent e)
    {
        if (string.IsNullOrWhiteSpace(e.OutcomeJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(e.OutcomeJson);
            if (doc.RootElement.TryGetProperty("approval", out var prop)
                && prop.TryGetDouble(out var val))
            {
                return val;
            }
        }
        catch (JsonException) { /* ignore malformed */ }
        return 0;
    }

    private static string BuildWeekSummary(int week, WeekResult result)
    {
        var c = result.Components;
        return $"Week {week}: approval {result.NewApproval:F1}% "
               + $"(net {c.NetChange:+0.0;-0.0}; ads {c.Advertising:+0.0;-0.0}, "
               + $"town halls {c.TownHall:+0.0;-0.0}, events {c.Event:+0.0;-0.0}, "
               + $"pressure {(-c.DifficultyPressure):+0.0;-0.0}).";
    }

    // ---------------------------------------------------------------- Mapping

    private static CampaignSummaryDto ToSummary(Campaign c) => new()
    {
        Id = c.Id,
        CandidateName = c.CandidateName,
        PersonaId = c.PersonaId,
        OpponentName = c.OpponentName,
        Theme = c.Theme,
        CurrentWeek = c.CurrentWeek,
        TotalWeeks = c.TotalWeeks,
        Difficulty = c.Difficulty.ToString(),
        Status = c.Status.ToString(),
        Approval = c.Approval,
        Won = c.Won,
        FinalApproval = c.FinalApproval,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        CompletedAt = c.CompletedAt,
    };

    private CampaignDetailDto ToDetail(Campaign c)
    {
        var milestoneDue = c.Status == CampaignStatus.Active
            && IsDebateMilestone(c.CurrentWeek)
            && c.LastResolvedDebateWeek < c.CurrentWeek;

        var activeDebateId = c.LastResolvedDebateWeek >= c.CurrentWeek
            ? _db.Debates
                .Where(d => d.CampaignId == c.Id && d.CampaignWeek == c.CurrentWeek)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefault()
            : null;

        return new CampaignDetailDto
        {
            Campaign = ToSummary(c),
            Resources = new CampaignResourcesDto
            {
                Budget = c.Resources.Budget,
                TimeUnits = c.Resources.TimeUnits,
                StaffCount = c.Resources.StaffCount,
                Momentum = c.Resources.Momentum,
            },
            CurrentApproval = c.Approval,
            Weeks = c.Weeks.OrderBy(w => w.WeekNumber).Select(ToWeekDto).ToList(),
            PendingEvents = c.Events
                .Where(e => !e.Resolved && e.WeekNumber == c.CurrentWeek)
                .OrderBy(e => e.CreatedAt)
                .Select(ToEventDto)
                .ToList(),
            DebateMilestoneDue = milestoneDue,
            ActiveDebateId = activeDebateId,
        };
    }

    private static CampaignWeekDto ToWeekDto(CampaignWeek w) => new()
    {
        WeekNumber = w.WeekNumber,
        ApprovalRating = w.ApprovalRating,
        DecisionsJson = w.DecisionsJson,
        ResourceChangesJson = w.ResourceChangesJson,
        DebateId = w.DebateId,
        Summary = w.Summary,
        CreatedAt = w.CreatedAt,
    };

    private static CampaignEventDto ToEventDto(CampaignEvent e)
    {
        var options = new List<CampaignEventOptionDto>();
        try
        {
            using var doc = JsonDocument.Parse(e.OptionsJson);
            foreach (var optionEl in doc.RootElement.EnumerateArray())
            {
                options.Add(new CampaignEventOptionDto
                {
                    Id = optionEl.GetProperty("id").GetString() ?? string.Empty,
                    Label = optionEl.GetProperty("label").GetString() ?? string.Empty,
                });
            }
        }
        catch (JsonException) { /* ignore malformed */ }

        return new CampaignEventDto
        {
            Id = e.Id,
            WeekNumber = e.WeekNumber,
            Type = e.Type.ToString(),
            EventKey = e.EventKey,
            Title = e.Title,
            Description = e.Description,
            Options = options,
            Resolved = e.Resolved,
            ResponseChosen = e.ResponseChosen,
        };
    }
}
