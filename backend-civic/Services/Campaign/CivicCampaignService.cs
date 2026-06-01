using System.Text.Json;
using Arena.Shared.Llm;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Scoped orchestrator for the Campaign Manager game mode. The player manages an existing
/// <see cref="VirtualCandidate"/> and tries to make them finish first in their race by election
/// day. Owns all DB persistence and drives the pure <see cref="CivicSupportModel"/> formulas.
/// Support simulation is local to the campaign and never mutates the global candidate catalog.
/// Post generation reuses <see cref="CampaignPostGenerationService"/> and is LLM-guarded (a
/// templated fallback is used when no Anthropic key is configured, so dev/tests never hit the net).
/// </summary>
public class CivicCampaignService
{
    private readonly CivicDbContext _db;
    private readonly CampaignPostGenerationService _postGenerator;
    private readonly CivicCampaignOptions _opts;
    private readonly ILogger<CivicCampaignService> _log;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions Json = new();

    public CivicCampaignService(
        CivicDbContext db,
        CampaignPostGenerationService postGenerator,
        IOptions<CivicCampaignOptions> opts,
        ILogger<CivicCampaignService> log)
    {
        _db = db;
        _postGenerator = postGenerator;
        _opts = opts.Value;
        _log = log;
    }

    // ---------------------------------------------------------------- Race picker

    public async Task<List<CivicRaceDto>> GetRacesAsync(CancellationToken ct = default)
    {
        var candidates = await _db.VirtualCandidates.OrderBy(c => c.Name).ToListAsync(ct);
        return candidates
            .GroupBy(c => RaceKeyFor(c))
            .Select(g =>
            {
                var first = g.First();
                return new CivicRaceDto
                {
                    RaceKey = g.Key,
                    Office = first.Office.ToString(),
                    State = first.State,
                    District = first.District,
                    Label = RaceLabel(first.Office, first.State, first.District),
                    Candidates = g.Select(c => c.ToSummaryDto()).ToList(),
                };
            })
            .OrderBy(r => r.Office).ThenBy(r => r.State).ThenBy(r => r.District)
            .ToList();
    }

    // ---------------------------------------------------------------- Create

    public async Task<CivicCampaignDetailDto> CreateAsync(string userId, CreateCivicCampaignRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CandidateSlug))
            throw new CivicCampaignValidationException("A candidate must be selected.");

        var candidate = await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .FirstOrDefaultAsync(c => c.Slug == req.CandidateSlug, ct)
            ?? throw new CivicCampaignValidationException($"Unknown candidate '{req.CandidateSlug}'.");

        var cycle = await _db.ElectionCycles
            .Where(c => c.IsCurrent)
            .OrderByDescending(c => c.ElectionDate)
            .FirstOrDefaultAsync(ct)
            ?? throw new CivicCampaignValidationException("There is no current election cycle.");

        var totalWeeks = req.TotalWeeks ?? _opts.DefaultTotalWeeks;
        totalWeeks = (int)CivicSupportModel.Clamp(totalWeeks, _opts.MinTotalWeeks, _opts.MaxTotalWeeks);

        var raceKey = RaceKeyFor(candidate);
        var raceCandidates = await RaceCandidatesAsync(candidate, ct);

        var campaign = new CivicCampaign
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CandidateId = candidate.Id,
            ElectionCycleId = cycle.Id,
            RaceKey = raceKey,
            RaceLabel = RaceLabel(candidate.Office, candidate.State, candidate.District),
            Difficulty = req.Difficulty,
            TotalWeeks = totalWeeks,
            CurrentWeek = 1,
            Status = CivicCampaignStatus.Active,
            ActionsRemaining = _opts.ActionsPerWeek,
        };
        _db.CivicCampaigns.Add(campaign);

        // Seed standings: even split, with a small incumbency bump that's renormalized to 100.
        var rawShares = raceCandidates.ToDictionary(
            c => c.Id,
            c => CivicSupportModel.EvenShare(raceCandidates.Count) + (c.IsIncumbent ? _opts.IncumbentBonus : 0));
        var shareSum = rawShares.Values.Sum();

        foreach (var c in raceCandidates)
        {
            _db.CivicCampaignStandings.Add(new CivicCampaignStanding
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                CandidateId = c.Id,
                IsPlayer = c.Id == candidate.Id,
                SupportShare = rawShares[c.Id] / shareSum * 100.0,
                Momentum = _opts.StartingMomentum,
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(userId, campaign.Id, ct);
    }

    // ---------------------------------------------------------------- List / detail

    public async Task<List<CivicCampaignSummaryDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var campaigns = await _db.CivicCampaigns
            .Where(c => c.UserId == userId)
            .Include(c => c.Candidate)
            .Include(c => c.Standings)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

        return campaigns.Select(c =>
        {
            var player = c.Standings.FirstOrDefault(s => s.IsPlayer);
            var leadShare = c.Standings.Count == 0 ? 0 : c.Standings.Max(s => s.SupportShare);
            return new CivicCampaignSummaryDto
            {
                Id = c.Id,
                CandidateSlug = c.Candidate?.Slug ?? "",
                CandidateName = c.Candidate?.Name ?? "",
                Party = c.Candidate?.Party ?? "",
                RaceLabel = c.RaceLabel,
                Difficulty = c.Difficulty.ToString(),
                Status = c.Status.ToString(),
                CurrentWeek = c.CurrentWeek,
                TotalWeeks = c.TotalWeeks,
                PlayerSupport = Math.Round(player?.SupportShare ?? 0, 1),
                IsLeading = player is not null && player.SupportShare >= leadShare - 0.001,
                Won = c.Won,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            };
        }).ToList();
    }

    public async Task<CivicCampaignDetailDto> GetDetailAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var salient = await SalientIssuesForCurrentWeekAsync(campaign, ct);
        var thisWeekActions = campaign.Actions.Where(a => a.WeekNumber == campaign.CurrentWeek).ToList();

        return await BuildDetailAsync(campaign, candidate, salient, thisWeekActions, ct);
    }

    // ---------------------------------------------------------------- Take action

    public async Task<TakeActionResult> TakeActionAsync(string userId, Guid id, TakeActionRequest req, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        if (campaign.Status != CivicCampaignStatus.Active)
            throw new CivicCampaignConflictException("This campaign is no longer active.");
        if (campaign.ActionsRemaining <= 0)
            throw new CivicCampaignConflictException("No action points left this week. Advance to the next week.");

        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var salient = await SalientIssuesForCurrentWeekAsync(campaign, ct);
        var playerStanding = campaign.Standings.First(s => s.IsPlayer);

        // Resolve the target issue for this action.
        var target = ResolveTarget(req, candidate, salient);
        var fit = CivicCampaignFit.IssueFit(candidate, target);
        var salienceWeight = CivicSalience.Weight(salient, target);

        var delta = CivicSupportModel.ActionPoints(req.ActionType, fit, salienceWeight, playerStanding.Momentum, _opts);

        // Optionally generate a real campaign post for PublishPost / RapidResponse actions.
        Guid? generatedPostId = null;
        string? generatedBody = null;
        if (req.ActionType is CivicCampaignActionType.PublishPost or CivicCampaignActionType.RapidResponse)
        {
            var tone = req.Tone ?? candidate.DefaultTone;
            var post = await TryGeneratePostAsync(candidate, target, tone, req.ActionType, ct);
            if (post is not null)
            {
                generatedPostId = post.Id;
                generatedBody = post.Body;
            }
        }

        var action = new CivicCampaignAction
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            WeekNumber = campaign.CurrentWeek,
            ActionType = req.ActionType,
            Target = target,
            Tone = req.Tone,
            SupportDelta = Math.Round(delta, 3),
            GeneratedPostId = generatedPostId,
            Summary = DescribeAction(req.ActionType, target, delta),
        };
        _db.CivicCampaignActions.Add(action);

        campaign.ActionsRemaining -= 1;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Reload for an up-to-date detail (incl. the new action).
        campaign = await LoadOwnedAsync(userId, id, ct);
        var thisWeekActions = campaign.Actions.Where(a => a.WeekNumber == campaign.CurrentWeek).ToList();
        var detail = await BuildDetailAsync(campaign, candidate, salient, thisWeekActions, ct);

        return new TakeActionResult
        {
            Action = ToActionDto(action),
            PlayerSupportAfter = Math.Round(playerStanding.SupportShare, 1),
            ActionsRemaining = campaign.ActionsRemaining,
            GeneratedPostBody = generatedBody,
            Campaign = detail,
        };
    }

    // ---------------------------------------------------------------- Advance week

    public async Task<AdvanceWeekResult> AdvanceWeekAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        if (campaign.Status != CivicCampaignStatus.Active)
            throw new CivicCampaignConflictException("This campaign is no longer active.");

        var raceCandidates = await RaceCandidatesByIdAsync(campaign, ct);
        var salient = await SalientIssuesForCurrentWeekAsync(campaign, ct);
        var weekActions = campaign.Actions.Where(a => a.WeekNumber == campaign.CurrentWeek).ToList();

        // Index-aligned arrays over the race's candidates.
        var standingsByCandidate = campaign.Standings.ToDictionary(s => s.CandidateId);
        var ordered = raceCandidates.Where(c => standingsByCandidate.ContainsKey(c.Id)).ToList();
        var current = ordered.Select(c => standingsByCandidate[c.Id].SupportShare).ToArray();
        var deltas = new double[ordered.Count];

        // Player's delta is the sum of this week's action deltas.
        var playerDelta = weekActions.Sum(a => a.SupportDelta);

        // Did the player play any defense (ShoreUpAxis) this week?
        var defended = weekActions.Any(a => a.ActionType == CivicCampaignActionType.ShoreUpAxis);
        var defenseFactor = defended ? _opts.ShoreUpAxisDefense : 1.0;

        for (var i = 0; i < ordered.Count; i++)
        {
            var c = ordered[i];
            var standing = standingsByCandidate[c.Id];
            if (standing.IsPlayer)
            {
                deltas[i] = playerDelta;
            }
            else
            {
                var oppFit = CivicCampaignFit.AverageFit(c, salient);
                var variance = (_random.NextDouble() * 2 - 1) * _opts.OpponentVariance;
                deltas[i] = CivicSupportModel.OpponentDelta(
                    campaign.Difficulty, oppFit, standing.Momentum, variance, defenseFactor, _opts);
            }
        }

        var newShares = CivicSupportModel.ApplyAndNormalize(current, deltas);

        // Persist new shares + momentum.
        for (var i = 0; i < ordered.Count; i++)
        {
            var standing = standingsByCandidate[ordered[i].Id];
            standing.SupportShare = Math.Round(newShares[i], 3);
            var gain = standing.IsPlayer ? Math.Max(0, playerDelta) : Math.Max(0, deltas[i]);
            standing.Momentum = Math.Round(CivicSupportModel.UpdateMomentum(standing.Momentum, gain * _opts.MomentumGainPerPoint / _opts.BaseActionPoints, _opts), 2);
            standing.UpdatedAt = DateTime.UtcNow;
        }

        var playerStanding = standingsByCandidate[campaign.CandidateId];
        var leadShare = newShares.Max();
        var isLeading = playerStanding.SupportShare >= leadShare - 0.001;

        // Snapshot the completed week.
        var standingsSnapshot = ordered.Select(c =>
        {
            var s = standingsByCandidate[c.Id];
            return new { c.Id, c.Name, c.Party, s.IsPlayer, Support = Math.Round(s.SupportShare, 2) };
        }).ToList();

        var summary = BuildWeekSummary(campaign.CurrentWeek, weekActions, playerStanding.SupportShare, isLeading);
        var week = new CivicCampaignWeek
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            WeekNumber = campaign.CurrentWeek,
            PlayerSupportAfter = Math.Round(playerStanding.SupportShare, 2),
            SalientIssuesJson = JsonSerializer.Serialize(salient, Json),
            StandingsJson = JsonSerializer.Serialize(standingsSnapshot, Json),
            DeltaBreakdownJson = JsonSerializer.Serialize(new { playerDelta = Math.Round(playerDelta, 3), defended }, Json),
            Summary = summary,
        };
        _db.CivicCampaignWeeks.Add(week);

        var completed = campaign.CurrentWeek >= campaign.TotalWeeks;
        if (completed)
        {
            FinalizeCampaign(campaign, ordered, standingsByCandidate);
        }
        else
        {
            campaign.CurrentWeek += 1;
            campaign.ActionsRemaining = _opts.ActionsPerWeek;
        }
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var reloaded = await LoadOwnedAsync(userId, id, ct);
        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var nextSalient = await SalientIssuesForCurrentWeekAsync(reloaded, ct);
        var thisWeekActions = reloaded.Actions.Where(a => a.WeekNumber == reloaded.CurrentWeek).ToList();
        var detail = await BuildDetailAsync(reloaded, candidate, nextSalient, thisWeekActions, ct);

        return new AdvanceWeekResult
        {
            CompletedWeek = week.WeekNumber,
            PlayerSupportAfter = Math.Round(playerStanding.SupportShare, 1),
            IsLeading = isLeading,
            Standings = detail.Standings,
            Summary = summary,
            CampaignCompleted = completed,
            Campaign = detail,
        };
    }

    // ---------------------------------------------------------------- Results

    public async Task<CivicCampaignResultsDto> GetResultsAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        if (campaign.Status != CivicCampaignStatus.Completed)
            throw new CivicCampaignValidationException("This campaign hasn't finished yet.");

        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var standings = await BuildStandingsAsync(campaign, ct);
        var player = standings.First(s => s.IsPlayer);
        var rank = standings.OrderByDescending(s => s.SupportShare).ToList().FindIndex(s => s.IsPlayer) + 1;

        var trend = campaign.Weeks
            .OrderBy(w => w.WeekNumber)
            .Select(ToWeekDto)
            .ToList();

        return new CivicCampaignResultsDto
        {
            Id = campaign.Id,
            CandidateName = candidate.Name,
            RaceLabel = campaign.RaceLabel,
            Won = campaign.Won ?? false,
            FinalSupport = Math.Round(campaign.FinalSupport ?? player.SupportShare, 1),
            FinalRank = rank,
            FieldSize = standings.Count,
            TotalWeeks = campaign.TotalWeeks,
            Outcome = campaign.Outcome ?? "",
            FinalStandings = standings.OrderByDescending(s => s.SupportShare).ToList(),
            SupportTrend = trend,
        };
    }

    // ---------------------------------------------------------------- Internals

    private void FinalizeCampaign(
        CivicCampaign campaign,
        List<VirtualCandidate> ordered,
        Dictionary<Guid, CivicCampaignStanding> standingsByCandidate)
    {
        var shares = ordered.Select(c => standingsByCandidate[c.Id].SupportShare).ToArray();
        var winnerIdx = CivicSupportModel.WinnerIndex(shares);
        var winner = ordered[winnerIdx];
        var player = standingsByCandidate[campaign.CandidateId];

        var won = winner.Id == campaign.CandidateId;
        var rank = ordered
            .OrderByDescending(c => standingsByCandidate[c.Id].SupportShare)
            .ToList()
            .FindIndex(c => c.Id == campaign.CandidateId) + 1;

        campaign.Status = CivicCampaignStatus.Completed;
        campaign.CompletedAt = DateTime.UtcNow;
        campaign.Won = won;
        campaign.FinalSupport = Math.Round(player.SupportShare, 1);
        campaign.ActionsRemaining = 0;
        campaign.Outcome = won
            ? $"Victory! Finished 1st with {player.SupportShare:0.0}% support."
            : $"Finished #{rank} of {ordered.Count} with {player.SupportShare:0.0}% support — the winner took {shares[winnerIdx]:0.0}%.";
    }

    private async Task<CampaignPost?> TryGeneratePostAsync(
        VirtualCandidate candidate, string issue, CampaignTone tone, CivicCampaignActionType actionType, CancellationToken ct)
    {
        // Reuse the real generation pipeline when possible; it throws LlmException with no API key.
        try
        {
            var trigger = actionType == CivicCampaignActionType.RapidResponse ? PostTrigger.Briefing : PostTrigger.Platform;
            var post = await _postGenerator.GenerateForCandidateAsync(candidate.Id, null, trigger, force: true, ct);
            if (post is not null) return post;
        }
        catch (LlmException ex)
        {
            _log.LogInformation("LLM unavailable ({Message}); using templated campaign post fallback.", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Campaign post generation failed; using templated fallback.");
        }

        // Deterministic templated fallback so the game is fully playable offline.
        return await CreateTemplatedPostAsync(candidate, issue, tone, actionType, ct);
    }

    private async Task<CampaignPost> CreateTemplatedPostAsync(
        VirtualCandidate candidate, string issue, CampaignTone tone, CivicCampaignActionType actionType, CancellationToken ct)
    {
        var plank = candidate.PlatformPlanks
            .FirstOrDefault(p => p.IssueTags.Any(t => string.Equals(t, issue, StringComparison.OrdinalIgnoreCase)))
            ?? candidate.PlatformPlanks.FirstOrDefault();

        var issueText = string.IsNullOrWhiteSpace(issue) ? "what matters most" : issue;
        var body = plank is not null
            ? $"On {issueText}: {Truncate(plank.Title, 120)}."
            : $"{candidate.Name} is fighting for {issueText}.";
        body = Truncate(body, 160);

        var post = new CampaignPost
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Body = body,
            Tone = tone,
            Intensity = candidate.DefaultIntensity,
            IssueTags = string.IsNullOrWhiteSpace(issue) ? Array.Empty<string>() : new[] { issue },
            Trigger = actionType == CivicCampaignActionType.RapidResponse ? PostTrigger.Briefing : PostTrigger.Platform,
            CitedReference = plank?.Title,
            CreatedAt = DateTime.UtcNow,
        };
        _db.CampaignPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return post;
    }

    private static string ResolveTarget(TakeActionRequest req, VirtualCandidate candidate, IReadOnlyList<string> salient)
    {
        if (!string.IsNullOrWhiteSpace(req.Target)) return req.Target!.Trim();
        // Default to the top salient issue the candidate is strongest on, else their first plank tag.
        var best = salient
            .OrderByDescending(i => CivicCampaignFit.IssueFit(candidate, i))
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(best)) return best!;
        return CivicCampaignFit.CandidateIssues(candidate).FirstOrDefault() ?? "";
    }

    private async Task<CivicCampaignDetailDto> BuildDetailAsync(
        CivicCampaign campaign, VirtualCandidate candidate, List<string> salient,
        List<CivicCampaignAction> thisWeekActions, CancellationToken ct)
    {
        var standings = await BuildStandingsAsync(campaign, ct);
        var history = campaign.Weeks.OrderBy(w => w.WeekNumber).Select(ToWeekDto).ToList();

        return new CivicCampaignDetailDto
        {
            Id = campaign.Id,
            CandidateSlug = candidate.Slug,
            CandidateName = candidate.Name,
            Party = candidate.Party,
            CandidateBio = candidate.Bio,
            AvatarBaseUrl = candidate.AvatarBaseUrl,
            RaceKey = campaign.RaceKey,
            RaceLabel = campaign.RaceLabel,
            Difficulty = campaign.Difficulty.ToString(),
            Status = campaign.Status.ToString(),
            CurrentWeek = campaign.CurrentWeek,
            TotalWeeks = campaign.TotalWeeks,
            ActionsRemaining = campaign.ActionsRemaining,
            Won = campaign.Won,
            FinalSupport = campaign.FinalSupport,
            Outcome = campaign.Outcome,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            Standings = standings.OrderByDescending(s => s.SupportShare).ToList(),
            SalientIssues = salient,
            AvailableActions = BuildActionOptions(campaign, candidate, salient),
            ThisWeekActions = thisWeekActions.OrderBy(a => a.CreatedAt).Select(ToActionDto).ToList(),
            History = history,
        };
    }

    private List<CivicActionOptionDto> BuildActionOptions(CivicCampaign campaign, VirtualCandidate candidate, IReadOnlyList<string> salient)
    {
        if (campaign.Status != CivicCampaignStatus.Active || campaign.ActionsRemaining <= 0)
            return new List<CivicActionOptionDto>();

        var topIssue = salient
            .OrderByDescending(i => CivicCampaignFit.IssueFit(candidate, i))
            .FirstOrDefault();

        return new List<CivicActionOptionDto>
        {
            new()
            {
                ActionType = nameof(CivicCampaignActionType.PublishPost),
                Label = "Publish a post",
                Description = "Put out a campaign message on an issue. Strong on-brand issues move the most support.",
                SuggestedTarget = topIssue,
            },
            new()
            {
                ActionType = nameof(CivicCampaignActionType.RapidResponse),
                Label = "Rapid response",
                Description = "Respond to the week's hottest issue. Higher risk and reward.",
                SuggestedTarget = salient.FirstOrDefault(),
            },
            new()
            {
                ActionType = nameof(CivicCampaignActionType.TargetIssue),
                Label = "Target an issue",
                Description = "Concentrate the week on one issue you own for a focus bonus.",
                SuggestedTarget = topIssue,
            },
            new()
            {
                ActionType = nameof(CivicCampaignActionType.ShoreUpAxis),
                Label = "Shore up a weakness",
                Description = "Play defense to blunt your opponents' gains this week.",
                SuggestedTarget = null,
            },
        };
    }

    private async Task<List<CivicCampaignStandingDto>> BuildStandingsAsync(CivicCampaign campaign, CancellationToken ct)
    {
        var candidateIds = campaign.Standings.Select(s => s.CandidateId).ToList();
        var candidates = await _db.VirtualCandidates
            .Where(c => candidateIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        return campaign.Standings.Select(s =>
        {
            candidates.TryGetValue(s.CandidateId, out var c);
            return new CivicCampaignStandingDto
            {
                CandidateId = s.CandidateId,
                CandidateSlug = c?.Slug ?? "",
                CandidateName = c?.Name ?? "",
                Party = c?.Party ?? "",
                IsPlayer = s.IsPlayer,
                SupportShare = Math.Round(s.SupportShare, 1),
                Momentum = Math.Round(s.Momentum, 1),
            };
        }).ToList();
    }

    private async Task<List<string>> SalientIssuesForCurrentWeekAsync(CivicCampaign campaign, CancellationToken ct)
    {
        var raceCandidates = await RaceCandidatesByIdAsync(campaign, ct);
        var seed = campaign.Id.GetHashCode();
        return CivicSalience.ForWeek(raceCandidates, campaign.CurrentWeek, seed);
    }

    private async Task<List<VirtualCandidate>> RaceCandidatesAsync(VirtualCandidate candidate, CancellationToken ct)
    {
        var all = await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Where(c => c.Office == candidate.Office && c.State == candidate.State && c.District == candidate.District)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return all;
    }

    private async Task<List<VirtualCandidate>> RaceCandidatesByIdAsync(CivicCampaign campaign, CancellationToken ct)
    {
        var ids = campaign.Standings.Select(s => s.CandidateId).ToList();
        return await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    private async Task<VirtualCandidate> LoadCandidateAsync(Guid candidateId, CancellationToken ct)
    {
        return await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .FirstOrDefaultAsync(c => c.Id == candidateId, ct)
            ?? throw new CivicCampaignNotFoundException("Managed candidate no longer exists.");
    }

    private async Task<CivicCampaign> LoadOwnedAsync(string userId, Guid id, CancellationToken ct)
    {
        var campaign = await _db.CivicCampaigns
            .Include(c => c.Standings)
            .Include(c => c.Weeks)
            .Include(c => c.Actions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (campaign is null || campaign.UserId != userId)
            throw new CivicCampaignNotFoundException();
        return campaign;
    }

    private static CivicCampaignActionDto ToActionDto(CivicCampaignAction a) => new()
    {
        WeekNumber = a.WeekNumber,
        ActionType = a.ActionType.ToString(),
        Target = a.Target,
        Tone = a.Tone?.ToString(),
        SupportDelta = Math.Round(a.SupportDelta, 2),
        GeneratedPostId = a.GeneratedPostId,
        Summary = a.Summary,
        CreatedAt = a.CreatedAt,
    };

    private static CivicCampaignWeekDto ToWeekDto(CivicCampaignWeek w) => new()
    {
        WeekNumber = w.WeekNumber,
        PlayerSupportAfter = Math.Round(w.PlayerSupportAfter, 1),
        SalientIssues = SafeDeserializeList(w.SalientIssuesJson),
        Summary = w.Summary,
        CreatedAt = w.CreatedAt,
    };

    private static List<string> SafeDeserializeList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json, Json) ?? new(); }
        catch { return new(); }
    }

    private static string DescribeAction(CivicCampaignActionType type, string target, double delta)
    {
        var sign = delta >= 0 ? "+" : "";
        var issue = string.IsNullOrWhiteSpace(target) ? "the campaign" : target;
        return type switch
        {
            CivicCampaignActionType.PublishPost => $"Published a post on {issue} ({sign}{delta:0.0} pts).",
            CivicCampaignActionType.RapidResponse => $"Rapid response on {issue} ({sign}{delta:0.0} pts).",
            CivicCampaignActionType.TargetIssue => $"Targeted {issue} ({sign}{delta:0.0} pts).",
            CivicCampaignActionType.ShoreUpAxis => $"Shored up a weakness ({sign}{delta:0.0} pts; opponents blunted).",
            _ => $"Action on {issue} ({sign}{delta:0.0} pts).",
        };
    }

    private static string BuildWeekSummary(int week, List<CivicCampaignAction> actions, double playerSupport, bool leading)
    {
        var lead = leading ? "leading the field" : "trailing the leader";
        if (actions.Count == 0)
            return $"Week {week}: a quiet week — no actions taken. Now at {playerSupport:0.0}% support, {lead}.";
        var gained = actions.Sum(a => a.SupportDelta);
        return $"Week {week}: {actions.Count} action(s) for {(gained >= 0 ? "+" : "")}{gained:0.0} pts. Now at {playerSupport:0.0}% support, {lead}.";
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max].TrimEnd();

    private static string RaceKeyFor(VirtualCandidate c)
    {
        var key = c.Office.ToString();
        if (!string.IsNullOrWhiteSpace(c.State)) key += $"/{c.State}";
        if (c.District is not null) key += $"/{c.District}";
        return key;
    }

    private static string RaceLabel(CandidateOffice office, string? state, int? district) => office switch
    {
        CandidateOffice.President => "President of the United States",
        CandidateOffice.Senate => $"U.S. Senate — {state}",
        CandidateOffice.House => $"U.S. House — {state}-{district}",
        _ => office.ToString(),
    };
}
