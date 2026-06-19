using System.Text.Json;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Coalition.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Civic.API.Services.Campaign;

/// <summary>
/// Scoped orchestrator for the Campaign Manager game mode. The player manages an existing
/// <see cref="VirtualCandidate"/> and tries to make them finish first in their race by election
/// day. The campaign is tied to the live national <see cref="Election"/> (the one the home-page
/// countdown shows) and runs in DAILY turns; the primary mechanic is responding to real incoming
/// news (synthesized <see cref="Briefing"/>s) with pre-generated, candidate-specific options.
///
/// All support simulation is local to the campaign and never mutates the global candidate catalog.
/// LLM use is guarded (<see cref="ILlmClient"/> throws when no key is configured) — every generation
/// path has a deterministic templated fallback, so dev/tests never hit the network.
/// </summary>
public class CivicCampaignService
{
    private readonly CivicDbContext _db;
    private readonly ICampaignPostFactory _postFactory;
    private readonly ICivicCatalog _catalog;
    private readonly CivicCampaignOptions _opts;
    private readonly ILogger<CivicCampaignService> _log;
    private readonly ReasoningLedger _ledger;
    private readonly Random _random = new();

    private static readonly JsonSerializerOptions Json = new();

    public CivicCampaignService(
        CivicDbContext db,
        ICampaignPostFactory postFactory,
        ICivicCatalog catalog,
        IOptions<CivicCampaignOptions> opts,
        ILogger<CivicCampaignService> log,
        ReasoningLedger ledger)
    {
        _db = db;
        _postFactory = postFactory;
        _catalog = catalog;
        _opts = opts.Value;
        _log = log;
        _ledger = ledger;
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

        // Snap to the next upcoming national election — the one the home-page countdown shows.
        var election = await NextElectionAsync(ct)
            ?? throw new CivicCampaignValidationException("There is no upcoming election to campaign for.");

        var electionDate = DateTime.SpecifyKind(election.ScheduledAt, DateTimeKind.Utc);
        var totalDays = ComputeTotalDays(DateTime.UtcNow, electionDate, _opts.MaxCampaignDays);

        var raceKey = RaceKeyFor(candidate);
        var raceCandidates = await RaceCandidatesAsync(candidate, ct);

        var campaign = new CivicCampaign
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CandidateId = candidate.Id,
            ElectionId = election.Id,
            ElectionName = election.Name,
            ElectionDate = electionDate,
            RaceKey = raceKey,
            RaceLabel = RaceLabel(candidate.Office, candidate.State, candidate.District),
            Difficulty = req.Difficulty,
            TotalDays = totalDays,
            CurrentDay = 1,
            Status = CivicCampaignStatus.Active,
            ActionsRemaining = _opts.ActionsPerDay,
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
                CurrentDay = c.CurrentDay,
                TotalDays = c.TotalDays,
                DaysRemaining = DaysRemaining(c.ElectionDate),
                ElectionName = c.ElectionName,
                ElectionDate = c.ElectionDate,
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
        var salient = await SalientIssuesForCurrentDayAsync(campaign, ct);
        var todayActions = campaign.Actions.Where(a => a.DayNumber == campaign.CurrentDay).ToList();
        return await BuildDetailAsync(campaign, candidate, salient, todayActions, ct);
    }

    // ---------------------------------------------------------------- News response page

    /// <summary>
    /// Data for the dedicated response page: the candidate's profile + values, the chosen news item,
    /// and each response option's full post text (so the manager reads exactly what would be said).
    /// </summary>
    public async Task<NewsResponsePageDto> GetNewsResponsePageAsync(
        string userId, Guid id, string briefingSlug, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        var candidate = await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Include(c => c.AxisScores)
            .FirstOrDefaultAsync(c => c.Id == campaign.CandidateId, ct)
            ?? throw new CivicCampaignNotFoundException("Managed candidate no longer exists.");

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == briefingSlug, ct)
            ?? throw new CivicCampaignValidationException($"Unknown news item '{briefingSlug}'.");

        var options = await _postFactory.GetOrCreateResponseOptionsAsync(candidate, briefing, ct);
        var alreadyResponded = campaign.Actions.Any(a =>
            a.ActionType == CivicCampaignActionType.RespondToNews && a.RespondedBriefingSlug == briefing.Slug);

        return new NewsResponsePageDto
        {
            CampaignId = campaign.Id,
            CandidateSlug = candidate.Slug,
            CandidateName = candidate.Name,
            Party = candidate.Party,
            CandidateBio = candidate.Bio,
            AvatarBaseUrl = candidate.AvatarBaseUrl,
            Values = candidate.AxisScores.ToValueDtos(_catalog),
            Platform = candidate.PlatformPlanks.Select(p => p.ToDto()).ToList(),
            BriefingSlug = briefing.Slug,
            Headline = briefing.Headline,
            Summary = briefing.Summary30,
            ValuesInConflict = briefing.ValuesInConflict.ToList(),
            Tags = briefing.Tags.ToList(),
            AlreadyResponded = alreadyResponded,
            ActionsRemaining = campaign.ActionsRemaining,
            Options = options.Select(o => new NewsResponseOptionDetailDto
            {
                Id = o.Id,
                Label = o.Label,
                Angle = o.Angle,
                Tone = o.Tone,
                Body = o.Body,
            }).ToList(),
        };
    }

    // ---------------------------------------------------------------- Take action

    public async Task<TakeActionResult> TakeActionAsync(string userId, Guid id, TakeActionRequest req, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        if (campaign.Status != CivicCampaignStatus.Active)
            throw new CivicCampaignConflictException("This campaign is no longer active.");
        if (campaign.ActionsRemaining <= 0)
            throw new CivicCampaignConflictException("No actions left today. Advance to the next day.");

        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var salient = await SalientIssuesForCurrentDayAsync(campaign, ct);
        var playerStanding = campaign.Standings.First(s => s.IsPlayer);

        CivicCampaignAction action;
        string? generatedBody = null;

        if (req.ActionType == CivicCampaignActionType.RespondToNews)
        {
            (action, generatedBody) = await HandleNewsResponseAsync(campaign, candidate, playerStanding, salient, req, ct);
        }
        else
        {
            action = HandleSecondaryAction(campaign, candidate, playerStanding, salient, req);
        }

        _db.CivicCampaignActions.Add(action);
        campaign.ActionsRemaining -= 1;
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Responding to a news item is real engagement — credit reasoning XP (capped/diminishing).
        if (action.ActionType == CivicCampaignActionType.RespondToNews)
            await _ledger.RecordAsync(userId, CoalitionActType.CampaignNewsResponse, ct: ct);

        campaign = await LoadOwnedAsync(userId, id, ct);
        var todayActions = campaign.Actions.Where(a => a.DayNumber == campaign.CurrentDay).ToList();
        var detail = await BuildDetailAsync(campaign, candidate, salient, todayActions, ct);

        return new TakeActionResult
        {
            Action = ToActionDto(action),
            PlayerSupportAfter = Math.Round(playerStanding.SupportShare, 1),
            ActionsRemaining = campaign.ActionsRemaining,
            GeneratedPostBody = generatedBody,
            Campaign = detail,
        };
    }

    private async Task<(CivicCampaignAction, string?)> HandleNewsResponseAsync(
        CivicCampaign campaign, VirtualCandidate candidate, CivicCampaignStanding playerStanding,
        IReadOnlyList<string> salient, TakeActionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.BriefingSlug))
            throw new CivicCampaignValidationException("A news item must be chosen to respond to.");
        if (string.IsNullOrWhiteSpace(req.OptionId))
            throw new CivicCampaignValidationException("A response option must be chosen.");

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == req.BriefingSlug, ct)
            ?? throw new CivicCampaignValidationException($"Unknown news item '{req.BriefingSlug}'.");

        var alreadyResponded = campaign.Actions.Any(a =>
            a.ActionType == CivicCampaignActionType.RespondToNews && a.RespondedBriefingSlug == briefing.Slug);
        if (alreadyResponded)
            throw new CivicCampaignConflictException("You've already responded to this news item.");

        var options = await _postFactory.GetOrCreateResponseOptionsAsync(candidate, briefing, ct);
        var chosen = options.FirstOrDefault(o => o.Id == req.OptionId)
            ?? throw new CivicCampaignValidationException("That response option is no longer available.");

        // Support delta: fit of the briefing's issues × salience × momentum × news multiplier.
        var briefingIssues = briefing.Tags.Concat(briefing.ValuesInConflict).ToList();
        var fit = CivicCampaignFit.AverageFit(candidate, briefingIssues);
        var salienceWeight = briefingIssues.Count == 0
            ? 0.6
            : briefingIssues.Max(i => CivicSalience.Weight(salient, i));
        var delta = CivicSupportModel.ActionPoints(
            CivicCampaignActionType.RespondToNews, fit, salienceWeight, playerStanding.Momentum, _opts);

        var tone = ParseTone(chosen.Tone) ?? req.Tone ?? candidate.DefaultTone;
        var post = await _postFactory.CreatePostFromBodyAsync(
            candidate, chosen.Body, tone, briefing, campaign.UserId, campaign.Id, ct);

        var action = new CivicCampaignAction
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            DayNumber = campaign.CurrentDay,
            ActionType = CivicCampaignActionType.RespondToNews,
            Target = briefingIssues.FirstOrDefault(),
            RespondedBriefingSlug = briefing.Slug,
            Tone = tone,
            SupportDelta = Math.Round(delta, 3),
            GeneratedPostId = post.Id,
            Summary = $"Responded to \"{Truncate(briefing.Headline, 80)}\" — {chosen.Label} ({Sign(delta)}{delta:0.0} pts).",
        };
        return (action, post.Body);
    }

    private CivicCampaignAction HandleSecondaryAction(
        CivicCampaign campaign, VirtualCandidate candidate, CivicCampaignStanding playerStanding,
        IReadOnlyList<string> salient, TakeActionRequest req)
    {
        // Only the secondary "budgeting tools" are allowed via this path.
        if (req.ActionType is not (CivicCampaignActionType.TargetIssue or CivicCampaignActionType.ShoreUpAxis))
            throw new CivicCampaignValidationException("Unsupported action. Respond to a news item, or use a budgeting tool.");

        var target = ResolveTarget(req, candidate, salient);
        var fit = CivicCampaignFit.IssueFit(candidate, target);
        var salienceWeight = CivicSalience.Weight(salient, target);
        var delta = CivicSupportModel.ActionPoints(req.ActionType, fit, salienceWeight, playerStanding.Momentum, _opts);

        return new CivicCampaignAction
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            DayNumber = campaign.CurrentDay,
            ActionType = req.ActionType,
            Target = target,
            Tone = req.Tone,
            SupportDelta = Math.Round(delta, 3),
            Summary = DescribeAction(req.ActionType, target, delta),
        };
    }

    // ---------------------------------------------------------------- Advance day

    public async Task<AdvanceDayResult> AdvanceDayAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var campaign = await LoadOwnedAsync(userId, id, ct);
        if (campaign.Status != CivicCampaignStatus.Active)
            throw new CivicCampaignConflictException("This campaign is no longer active.");

        var raceCandidates = await RaceCandidatesByIdAsync(campaign, ct);
        var salient = await SalientIssuesForCurrentDayAsync(campaign, ct);
        var dayActions = campaign.Actions.Where(a => a.DayNumber == campaign.CurrentDay).ToList();

        var standingsByCandidate = campaign.Standings.ToDictionary(s => s.CandidateId);
        var ordered = raceCandidates.Where(c => standingsByCandidate.ContainsKey(c.Id)).ToList();
        var current = ordered.Select(c => standingsByCandidate[c.Id].SupportShare).ToArray();
        var deltas = new double[ordered.Count];

        var playerDelta = dayActions.Sum(a => a.SupportDelta);
        var defended = dayActions.Any(a => a.ActionType == CivicCampaignActionType.ShoreUpAxis);
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

        for (var i = 0; i < ordered.Count; i++)
        {
            var standing = standingsByCandidate[ordered[i].Id];
            standing.SupportShare = Math.Round(newShares[i], 3);
            var gain = standing.IsPlayer ? Math.Max(0, playerDelta) : Math.Max(0, deltas[i]);
            standing.Momentum = Math.Round(
                CivicSupportModel.UpdateMomentum(standing.Momentum, gain * _opts.MomentumGainPerPoint / _opts.BaseActionPoints, _opts), 2);
            standing.UpdatedAt = DateTime.UtcNow;
        }

        var playerStanding = standingsByCandidate[campaign.CandidateId];
        var leadShare = newShares.Max();
        var isLeading = playerStanding.SupportShare >= leadShare - 0.001;

        var standingsSnapshot = ordered.Select(c =>
        {
            var s = standingsByCandidate[c.Id];
            return new { c.Id, c.Name, c.Party, s.IsPlayer, Support = Math.Round(s.SupportShare, 2) };
        }).ToList();

        var summary = BuildDaySummary(campaign.CurrentDay, dayActions, playerStanding.SupportShare, isLeading);
        var day = new CivicCampaignWeek
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            DayNumber = campaign.CurrentDay,
            PlayerSupportAfter = Math.Round(playerStanding.SupportShare, 2),
            SalientIssuesJson = JsonSerializer.Serialize(salient, Json),
            StandingsJson = JsonSerializer.Serialize(standingsSnapshot, Json),
            DeltaBreakdownJson = JsonSerializer.Serialize(new { playerDelta = Math.Round(playerDelta, 3), defended }, Json),
            Summary = summary,
        };
        _db.CivicCampaignWeeks.Add(day);

        var completed = campaign.CurrentDay >= campaign.TotalDays;
        if (completed)
        {
            FinalizeCampaign(campaign, ordered, standingsByCandidate);
        }
        else
        {
            campaign.CurrentDay += 1;
            campaign.ActionsRemaining = _opts.ActionsPerDay;
        }
        campaign.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var reloaded = await LoadOwnedAsync(userId, id, ct);
        var candidate = await LoadCandidateAsync(campaign.CandidateId, ct);
        var nextSalient = await SalientIssuesForCurrentDayAsync(reloaded, ct);
        var todayActions = reloaded.Actions.Where(a => a.DayNumber == reloaded.CurrentDay).ToList();
        var detail = await BuildDetailAsync(reloaded, candidate, nextSalient, todayActions, ct);

        return new AdvanceDayResult
        {
            CompletedDay = day.DayNumber,
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

        var trend = campaign.Weeks.OrderBy(w => w.DayNumber).Select(ToWeekDto).ToList();

        return new CivicCampaignResultsDto
        {
            Id = campaign.Id,
            CandidateName = candidate.Name,
            RaceLabel = campaign.RaceLabel,
            Won = campaign.Won ?? false,
            FinalSupport = Math.Round(campaign.FinalSupport ?? player.SupportShare, 1),
            FinalRank = rank,
            FieldSize = standings.Count,
            TotalWeeks = campaign.TotalDays,
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

    private static string ResolveTarget(TakeActionRequest req, VirtualCandidate candidate, IReadOnlyList<string> salient)
    {
        if (!string.IsNullOrWhiteSpace(req.Target)) return req.Target!.Trim();
        var best = salient.OrderByDescending(i => CivicCampaignFit.IssueFit(candidate, i)).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(best)) return best!;
        return CivicCampaignFit.CandidateIssues(candidate).FirstOrDefault() ?? "";
    }

    private async Task<CivicCampaignDetailDto> BuildDetailAsync(
        CivicCampaign campaign, VirtualCandidate candidate, List<string> salient,
        List<CivicCampaignAction> todayActions, CancellationToken ct)
    {
        var standings = await BuildStandingsAsync(campaign, ct);
        var history = campaign.Weeks.OrderBy(w => w.DayNumber).Select(ToWeekDto).ToList();
        var newsItems = await BuildNewsItemsAsync(campaign, candidate, ct);

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
            ElectionName = campaign.ElectionName,
            ElectionDate = campaign.ElectionDate,
            DaysRemaining = DaysRemaining(campaign.ElectionDate),
            CurrentDay = campaign.CurrentDay,
            TotalDays = campaign.TotalDays,
            ActionsRemaining = campaign.ActionsRemaining,
            Won = campaign.Won,
            FinalSupport = campaign.FinalSupport,
            Outcome = campaign.Outcome,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            Standings = standings.OrderByDescending(s => s.SupportShare).ToList(),
            SalientIssues = salient,
            NewsItems = newsItems,
            AvailableActions = BuildActionOptions(campaign, candidate, salient),
            TodayActions = todayActions.OrderBy(a => a.CreatedAt).Select(ToActionDto).ToList(),
            History = history,
        };
    }

    /// <summary>
    /// Surface the most recent news briefings this campaign hasn't responded to, each with the
    /// candidate's pre-generated (lazily cached) response options. Only built while the campaign is
    /// active and has actions left.
    /// </summary>
    private async Task<List<CampaignNewsItemDto>> BuildNewsItemsAsync(
        CivicCampaign campaign, VirtualCandidate candidate, CancellationToken ct)
    {
        if (campaign.Status != CivicCampaignStatus.Active || campaign.ActionsRemaining <= 0)
            return new List<CampaignNewsItemDto>();

        var respondedSlugs = campaign.Actions
            .Where(a => a.ActionType == CivicCampaignActionType.RespondToNews && a.RespondedBriefingSlug != null)
            .Select(a => a.RespondedBriefingSlug!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var recent = await _db.Briefings
            .OrderByDescending(b => b.CreatedAt)
            .Take(_opts.NewsItemsToOffer + respondedSlugs.Count)
            .ToListAsync(ct);

        var offer = recent
            .Where(b => !respondedSlugs.Contains(b.Slug))
            .Take(_opts.NewsItemsToOffer)
            .ToList();

        var result = new List<CampaignNewsItemDto>();
        foreach (var b in offer)
        {
            var options = await _postFactory.GetOrCreateResponseOptionsAsync(candidate, b, ct);
            result.Add(new CampaignNewsItemDto
            {
                BriefingSlug = b.Slug,
                Headline = b.Headline,
                Summary = b.Summary30,
                ValuesInConflict = b.ValuesInConflict.ToList(),
                Tags = b.Tags.ToList(),
                Options = options.Select(o => new NewsResponseOptionDto
                {
                    Id = o.Id,
                    Label = o.Label,
                    Angle = o.Angle,
                    Tone = o.Tone,
                }).ToList(),
            });
        }
        return result;
    }

    private List<CivicActionOptionDto> BuildActionOptions(CivicCampaign campaign, VirtualCandidate candidate, IReadOnlyList<string> salient)
    {
        if (campaign.Status != CivicCampaignStatus.Active || campaign.ActionsRemaining <= 0)
            return new List<CivicActionOptionDto>();

        var topIssue = salient.OrderByDescending(i => CivicCampaignFit.IssueFit(candidate, i)).FirstOrDefault();

        return new List<CivicActionOptionDto>
        {
            new()
            {
                ActionType = nameof(CivicCampaignActionType.TargetIssue),
                Label = "Target an issue",
                Description = "Spend the day concentrating on one issue you own for a focus bonus.",
                SuggestedTarget = topIssue,
            },
            new()
            {
                ActionType = nameof(CivicCampaignActionType.ShoreUpAxis),
                Label = "Shore up a weakness",
                Description = "Play defense to blunt your opponents' gains today.",
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

    private async Task<List<string>> SalientIssuesForCurrentDayAsync(CivicCampaign campaign, CancellationToken ct)
    {
        var raceCandidates = await RaceCandidatesByIdAsync(campaign, ct);
        var seed = campaign.Id.GetHashCode();
        return CivicSalience.ForWeek(raceCandidates, campaign.CurrentDay, seed);
    }

    private async Task<Election?> NextElectionAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Prefer the next upcoming National election (matches the home countdown). Fall back to any
        // upcoming election, then to the latest-scheduled one if none are in the future.
        return await _db.Elections
                   .Where(e => e.ScheduledAt >= now && e.Scope == ElectionScope.National)
                   .OrderBy(e => e.ScheduledAt)
                   .FirstOrDefaultAsync(ct)
               ?? await _db.Elections
                   .Where(e => e.ScheduledAt >= now)
                   .OrderBy(e => e.ScheduledAt)
                   .FirstOrDefaultAsync(ct)
               ?? await _db.Elections
                   .OrderByDescending(e => e.ScheduledAt)
                   .FirstOrDefaultAsync(ct);
    }

    private async Task<List<VirtualCandidate>> RaceCandidatesAsync(VirtualCandidate candidate, CancellationToken ct)
    {
        return await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .Where(c => c.Office == candidate.Office && c.State == candidate.State && c.District == candidate.District)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
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
        DayNumber = a.DayNumber,
        ActionType = a.ActionType.ToString(),
        Target = a.Target,
        RespondedBriefingSlug = a.RespondedBriefingSlug,
        Tone = a.Tone?.ToString(),
        SupportDelta = Math.Round(a.SupportDelta, 2),
        GeneratedPostId = a.GeneratedPostId,
        Summary = a.Summary,
        CreatedAt = a.CreatedAt,
    };

    private static CivicCampaignWeekDto ToWeekDto(CivicCampaignWeek w) => new()
    {
        DayNumber = w.DayNumber,
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

    private static CampaignTone? ParseTone(string? s)
        => Enum.TryParse<CampaignTone>(s, ignoreCase: true, out var t) ? t : null;

    private static int ComputeTotalDays(DateTime start, DateTime electionDate, int maxDays)
    {
        var days = (int)Math.Ceiling((electionDate.Date - start.Date).TotalDays);
        return Math.Clamp(days, 1, maxDays);
    }

    private static int DaysRemaining(DateTime electionDate)
        => Math.Max(0, (int)Math.Ceiling((electionDate.Date - DateTime.UtcNow.Date).TotalDays));

    private static string Sign(double d) => d >= 0 ? "+" : "";

    private static string DescribeAction(CivicCampaignActionType type, string target, double delta)
    {
        var sign = Sign(delta);
        var issue = string.IsNullOrWhiteSpace(target) ? "the campaign" : target;
        return type switch
        {
            CivicCampaignActionType.TargetIssue => $"Targeted {issue} ({sign}{delta:0.0} pts).",
            CivicCampaignActionType.ShoreUpAxis => $"Shored up a weakness ({sign}{delta:0.0} pts; opponents blunted).",
            CivicCampaignActionType.RespondToNews => $"Responded to the news ({sign}{delta:0.0} pts).",
            _ => $"Action on {issue} ({sign}{delta:0.0} pts).",
        };
    }

    private static string BuildDaySummary(int day, List<CivicCampaignAction> actions, double playerSupport, bool leading)
    {
        var lead = leading ? "leading the field" : "trailing the leader";
        if (actions.Count == 0)
            return $"Day {day}: a quiet day — no actions taken. Now at {playerSupport:0.0}% support, {lead}.";
        var gained = actions.Sum(a => a.SupportDelta);
        return $"Day {day}: {actions.Count} action(s) for {(gained >= 0 ? "+" : "")}{gained:0.0} pts. Now at {playerSupport:0.0}% support, {lead}.";
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
