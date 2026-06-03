using System.Text.Json;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Services.Campaign;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Leagues;

/// <summary>
/// Drives shared head-to-head rounds: the owner opens a round on a news briefing, members respond
/// with their candidate (reusing the Campaign Manager response pipeline), members vote on each
/// other's responses, and the owner closes the round to award points. Round responses and votes
/// reuse <see cref="CampaignPost"/> + <see cref="ICampaignReactionService"/>; only attribution and
/// points live here.
/// </summary>
public class LeagueRoundService
{
    private readonly CivicDbContext _db;
    private readonly ICampaignPostFactory _postFactory;
    private readonly ICampaignReactionService _reactions;

    private static readonly JsonSerializerOptions Json = new();

    public LeagueRoundService(
        CivicDbContext db,
        ICampaignPostFactory postFactory,
        ICampaignReactionService reactions)
    {
        _db = db;
        _postFactory = postFactory;
        _reactions = reactions;
    }

    // ---------------------------------------------------------------- List / detail

    public async Task<List<LeagueRoundSummaryDto>> ListRoundsAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireMember(league, userId);

        var memberById = league.Members.ToDictionary(m => m.Id);
        return league.Rounds
            .OrderByDescending(r => r.RoundNumber)
            .Select(r => r.ToSummaryDto(userId, WinnerName(r, memberById)))
            .ToList();
    }

    public async Task<LeagueRoundDetailDto> GetRoundAsync(string userId, Guid leagueId, Guid roundId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);
        var round = RequireRound(league, roundId);
        return await BuildRoundDetailAsync(league, round, me, ct);
    }

    // ---------------------------------------------------------------- Owner lifecycle

    public async Task<LeagueRoundDetailDto> OpenRoundAsync(string userId, Guid leagueId, OpenRoundRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireOwner(league, userId);

        if (string.IsNullOrWhiteSpace(req.BriefingSlug))
            throw new LeagueValidationException("Choose a news item for the round.");

        if (league.Rounds.Any(r => r.Status != LeagueRoundStatus.Closed))
            throw new LeagueConflictException("There's already an open round. Close it before starting another.");

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == req.BriefingSlug, ct)
            ?? throw new LeagueValidationException($"Unknown news item '{req.BriefingSlug}'.");

        var nextNumber = league.Rounds
            .Where(r => r.SeasonNumber == league.SeasonNumber)
            .Select(r => r.RoundNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var round = new LeagueRound
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            SeasonNumber = league.SeasonNumber,
            RoundNumber = nextNumber,
            BriefingSlug = briefing.Slug,
            Headline = briefing.Headline,
            Status = LeagueRoundStatus.OpenForResponses,
            OpensAt = DateTime.UtcNow,
            ResponsesCloseAt = req.ResponsesCloseAt,
            VotingCloseAt = req.VotingCloseAt,
        };
        _db.LeagueRounds.Add(round);
        league.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // EF relationship fixup already adds `round` to league.Rounds (the league is tracked), so we
        // must not add it again here or it would be double-counted.
        return await BuildRoundDetailAsync(league, round, me, ct);
    }

    public async Task<LeagueRoundDetailDto> StartVotingAsync(string userId, Guid leagueId, Guid roundId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireOwner(league, userId);
        var round = RequireRound(league, roundId);

        if (round.Status != LeagueRoundStatus.OpenForResponses)
            throw new LeagueConflictException("Voting can only start while a round is open for responses.");

        round.Status = LeagueRoundStatus.Voting;
        round.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await BuildRoundDetailAsync(league, round, me, ct);
    }

    public async Task<LeagueRoundResultsDto> CloseRoundAsync(string userId, Guid leagueId, Guid roundId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireOwner(league, userId);
        var round = RequireRound(league, roundId);

        if (round.Status == LeagueRoundStatus.Closed)
            throw new LeagueConflictException("This round is already closed.");
        if (round.Status != LeagueRoundStatus.Voting)
            throw new LeagueConflictException("Start voting before closing the round.");

        var posts = await LoadEntryPostsAsync(round, ct);
        var memberById = league.Members.ToDictionary(m => m.Id);

        // Net = whole-post (up - down). Highest net wins; ties all win.
        int Net(LeagueRoundEntry e) => posts.TryGetValue(e.PostId, out var p) ? p.UpCount - p.DownCount : 0;
        var bestNet = round.Entries.Count == 0 ? 0 : round.Entries.Max(Net);

        var awarded = new Dictionary<string, int>();
        Guid? singleWinner = null;
        var winnerCount = 0;

        foreach (var entry in round.Entries)
        {
            var isWinner = round.Entries.Count > 0 && Net(entry) == bestNet;
            var points = isWinner ? LeagueScoringService.WinnerPoints : LeagueScoringService.EntrantPoints;
            entry.PointsEarned = points;
            awarded[entry.Id.ToString()] = points;

            if (memberById.TryGetValue(entry.LeagueMemberId, out var member))
                member.SeasonPoints += points;

            if (isWinner)
            {
                winnerCount++;
                singleWinner = entry.LeagueMemberId;
            }
        }

        round.WinnerMemberId = winnerCount == 1 ? singleWinner : null;
        round.PointsAwardedJson = JsonSerializer.Serialize(awarded, Json);
        round.Status = LeagueRoundStatus.Closed;
        round.UpdatedAt = DateTime.UtcNow;
        league.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return BuildResults(league, round, posts, memberById);
    }

    public async Task<LeagueRoundResultsDto> GetResultsAsync(string userId, Guid leagueId, Guid roundId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireMember(league, userId);
        var round = RequireRound(league, roundId);
        if (round.Status != LeagueRoundStatus.Closed)
            throw new LeagueValidationException("This round hasn't been closed yet.");

        var posts = await LoadEntryPostsAsync(round, ct);
        var memberById = league.Members.ToDictionary(m => m.Id);
        return BuildResults(league, round, posts, memberById);
    }

    // ---------------------------------------------------------------- Submit + vote

    public async Task<LeagueRoundDetailDto> SubmitEntryAsync(
        string userId, Guid leagueId, Guid roundId, SubmitRoundEntryRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);
        var round = RequireRound(league, roundId);

        if (round.Status != LeagueRoundStatus.OpenForResponses)
            throw new LeagueConflictException("This round is no longer accepting responses.");
        if (me.CandidateId is null)
            throw new LeagueValidationException("Link a campaign to your league profile before entering a round.");
        if (round.Entries.Any(e => e.LeagueMemberId == me.Id))
            throw new LeagueConflictException("You've already entered this round.");
        if (string.IsNullOrWhiteSpace(req.OptionId))
            throw new LeagueValidationException("Choose a response option.");

        var candidate = await _db.VirtualCandidates
            .Include(c => c.PlatformPlanks)
            .Include(c => c.Sources)
            .FirstOrDefaultAsync(c => c.Id == me.CandidateId, ct)
            ?? throw new LeagueValidationException("Your linked candidate no longer exists.");

        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == round.BriefingSlug, ct)
            ?? throw new LeagueValidationException("The round's news item is no longer available.");

        var options = await _postFactory.GetOrCreateResponseOptionsAsync(candidate, briefing, ct);
        var chosen = options.FirstOrDefault(o => o.Id == req.OptionId)
            ?? throw new LeagueValidationException("That response option is no longer available.");

        var tone = CampaignPostFactory.ParseTone(chosen.Tone)
            ?? CampaignPostFactory.ParseTone(req.Tone)
            ?? candidate.DefaultTone;

        var post = await _postFactory.CreatePostFromBodyAsync(
            candidate, chosen.Body, tone, briefing, me.UserId, me.CampaignId, ct);

        var entry = new LeagueRoundEntry
        {
            Id = Guid.NewGuid(),
            LeagueRoundId = round.Id,
            LeagueMemberId = me.Id,
            UserId = me.UserId,
            CandidateId = candidate.Id,
            PostId = post.Id,
            OptionId = chosen.Id,
            OptionLabel = chosen.Label,
            Tone = tone,
        };
        _db.LeagueRoundEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        // EF relationship fixup already adds `entry` to round.Entries (the round is tracked), so we
        // must not add it again here or it would be double-counted.
        return await BuildRoundDetailAsync(league, round, me, ct);
    }

    public async Task<ReactionResultDto> VoteEntryAsync(
        string userId, Guid leagueId, Guid roundId, Guid entryId, ReactionRequestDto req, CancellationToken ct = default)
    {
        var (round, entry) = await LoadVotableEntryAsync(userId, leagueId, roundId, entryId, ct);
        if (round.Status != LeagueRoundStatus.Voting)
            throw new LeagueConflictException("Voting isn't open for this round.");
        if (entry.UserId == userId)
            throw new LeagueValidationException("You can't vote on your own response.");

        var type = ParseReaction(req?.Type);
        var result = await _reactions.ReactAsync(userId, entry.PostId, null, type, ct);
        return ToReactionDto(entry.PostId, result);
    }

    public async Task<ReactionResultDto> UnvoteEntryAsync(
        string userId, Guid leagueId, Guid roundId, Guid entryId, CancellationToken ct = default)
    {
        var (round, entry) = await LoadVotableEntryAsync(userId, leagueId, roundId, entryId, ct);
        if (round.Status != LeagueRoundStatus.Voting)
            throw new LeagueConflictException("Voting isn't open for this round.");

        var result = await _reactions.RemoveAsync(userId, entry.PostId, null, ct);
        return ToReactionDto(entry.PostId, result);
    }

    // ---------------------------------------------------------------- Build DTOs

    private async Task<LeagueRoundDetailDto> BuildRoundDetailAsync(
        League league, LeagueRound round, LeagueMember me, CancellationToken ct)
    {
        var briefing = await _db.Briefings.FirstOrDefaultAsync(b => b.Slug == round.BriefingSlug, ct);
        var alreadyEntered = round.Entries.Any(e => e.LeagueMemberId == me.Id);
        var open = round.Status == LeagueRoundStatus.OpenForResponses;

        // Can-submit gate + reason.
        bool canSubmit = open && me.CandidateId is not null && !alreadyEntered;
        string? cannotReason = null;
        if (!open) cannotReason = "Submissions are closed.";
        else if (me.CandidateId is null) cannotReason = "Link a campaign to your league profile to enter.";
        else if (alreadyEntered) cannotReason = "You've entered this round.";

        // Options only when the member can actually submit (avoids needless generation).
        var options = new List<NewsResponseOptionDetailDto>();
        if (canSubmit && briefing is not null && me.CandidateId is Guid candId)
        {
            var candidate = await _db.VirtualCandidates
                .Include(c => c.PlatformPlanks)
                .Include(c => c.Sources)
                .FirstOrDefaultAsync(c => c.Id == candId, ct);
            if (candidate is not null)
            {
                var opts = await _postFactory.GetOrCreateResponseOptionsAsync(candidate, briefing, ct);
                options = opts.Select(o => new NewsResponseOptionDetailDto
                {
                    Id = o.Id,
                    Label = o.Label,
                    Angle = o.Angle,
                    Tone = o.Tone,
                    Body = o.Body,
                }).ToList();
            }
        }

        var entriesVisible = alreadyEntered || !open;
        var memberById = league.Members.ToDictionary(m => m.Id);
        var entries = new List<LeagueRoundEntryDto>();
        if (entriesVisible)
        {
            var posts = await LoadEntryPostsAsync(round, ct);
            entries = BuildEntryDtos(round, me.UserId, posts, memberById, briefing);
        }

        return new LeagueRoundDetailDto
        {
            Id = round.Id,
            LeagueId = league.Id,
            RoundNumber = round.RoundNumber,
            Status = round.Status.ToString(),
            MyRole = me.Role.ToString(),
            BriefingSlug = round.BriefingSlug,
            Headline = briefing?.Headline ?? round.Headline,
            Summary = briefing?.Summary30 ?? "",
            ValuesInConflict = briefing?.ValuesInConflict.ToList() ?? new(),
            Tags = briefing?.Tags.ToList() ?? new(),
            ResponsesCloseAt = round.ResponsesCloseAt,
            VotingCloseAt = round.VotingCloseAt,
            IHaveEntered = alreadyEntered,
            CanSubmit = canSubmit,
            CannotSubmitReason = cannotReason,
            Options = options,
            EntriesVisible = entriesVisible,
            Entries = entries,
            WinnerMemberId = round.WinnerMemberId,
            WinnerDisplayName = WinnerName(round, memberById),
        };
    }

    private LeagueRoundResultsDto BuildResults(
        League league, LeagueRound round, IReadOnlyDictionary<Guid, CampaignPost> posts,
        IReadOnlyDictionary<Guid, LeagueMember> memberById)
    {
        return new LeagueRoundResultsDto
        {
            Id = round.Id,
            RoundNumber = round.RoundNumber,
            Headline = round.Headline,
            WinnerMemberId = round.WinnerMemberId,
            WinnerDisplayName = WinnerName(round, memberById),
            Entries = BuildEntryDtos(round, null, posts, memberById, null),
        };
    }

    private static List<LeagueRoundEntryDto> BuildEntryDtos(
        LeagueRound round, string? meUserId, IReadOnlyDictionary<Guid, CampaignPost> posts,
        IReadOnlyDictionary<Guid, LeagueMember> memberById, Briefing? briefing)
    {
        int Net(LeagueRoundEntry e) => posts.TryGetValue(e.PostId, out var p) ? p.UpCount - p.DownCount : 0;
        var bestNet = round.Entries.Count == 0 ? 0 : round.Entries.Max(Net);
        var closed = round.Status == LeagueRoundStatus.Closed;

        // Once voting/closed, rank by net so the strongest response sits on top; while open, keep
        // submission order so nobody reads the running tally as a verdict.
        var ordered = round.Status == LeagueRoundStatus.OpenForResponses
            ? round.Entries.OrderBy(e => e.CreatedAt)
            : round.Entries.OrderByDescending(Net).ThenBy(e => e.CreatedAt);

        return ordered.Select(e =>
        {
            memberById.TryGetValue(e.LeagueMemberId, out var member);
            posts.TryGetValue(e.PostId, out var post);
            var candidate = post?.Candidate ?? member?.Candidate;
            var net = Net(e);
            return new LeagueRoundEntryDto
            {
                Id = e.Id,
                MemberId = e.LeagueMemberId,
                DisplayName = member?.DisplayName ?? "Member",
                AvatarUrl = member?.AvatarUrl,
                IsMe = meUserId is not null && e.UserId == meUserId,
                OptionLabel = e.OptionLabel,
                PointsEarned = e.PointsEarned,
                Net = net,
                IsWinner = closed && round.Entries.Count > 0 && net == bestNet,
                Post = post?.ToDto(candidate, briefing?.Headline, briefing?.Summary30) ?? new CampaignPostDto(),
            };
        }).ToList();
    }

    // ---------------------------------------------------------------- Loaders / helpers

    private async Task<League> LoadLeagueAsync(Guid leagueId, CancellationToken ct)
    {
        return await _db.Leagues
            .Include(l => l.Members).ThenInclude(m => m.Candidate)
            .Include(l => l.Rounds).ThenInclude(r => r.Entries)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw new LeagueNotFoundException();
    }

    /// <summary>Loads the entries' posts (with candidate + fragments) keyed by post id.</summary>
    private async Task<Dictionary<Guid, CampaignPost>> LoadEntryPostsAsync(LeagueRound round, CancellationToken ct)
    {
        var postIds = round.Entries.Select(e => e.PostId).ToList();
        if (postIds.Count == 0) return new();
        // Loaded explicitly by id — bypasses the per-owner feed tailoring so every member's response
        // is visible to the whole league.
        return await _db.CampaignPosts
            .Where(p => postIds.Contains(p.Id))
            .Include(p => p.Candidate)
            .Include(p => p.Fragments)
            .ToDictionaryAsync(p => p.Id, ct);
    }

    private async Task<(LeagueRound round, LeagueRoundEntry entry)> LoadVotableEntryAsync(
        string userId, Guid leagueId, Guid roundId, Guid entryId, CancellationToken ct)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireMember(league, userId);
        var round = RequireRound(league, roundId);
        var entry = round.Entries.FirstOrDefault(e => e.Id == entryId)
            ?? throw new LeagueNotFoundException("Entry not found.");
        return (round, entry);
    }

    private static LeagueMember RequireMember(League league, string userId)
        => league.Members.FirstOrDefault(m => m.UserId == userId)
           ?? throw new LeagueNotFoundException();

    private static LeagueMember RequireOwner(League league, string userId)
    {
        var me = RequireMember(league, userId);
        if (me.Role != LeagueMemberRole.Owner)
            throw new LeagueForbiddenException();
        return me;
    }

    private static LeagueRound RequireRound(League league, Guid roundId)
        => league.Rounds.FirstOrDefault(r => r.Id == roundId)
           ?? throw new LeagueNotFoundException("Round not found.");

    private static string? WinnerName(LeagueRound r, IReadOnlyDictionary<Guid, LeagueMember> memberById)
        => r.WinnerMemberId is Guid wid && memberById.TryGetValue(wid, out var w) ? w.DisplayName : null;

    private static ReactionType ParseReaction(string? s)
        => Enum.TryParse<ReactionType>(s, ignoreCase: true, out var t) ? t : ReactionType.Up;

    private static ReactionResultDto ToReactionDto(Guid postId, ReactionResult r) => new()
    {
        PostId = postId,
        FragmentId = null,
        PostUp = r.Post.Up,
        PostDown = r.Post.Down,
        FragmentUp = r.Fragment?.Up,
        FragmentDown = r.Fragment?.Down,
    };
}
