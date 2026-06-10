using Civic.API.Models;
using Civic.API.Models.DTOs;

namespace Civic.API.Mapping;

public static class LeagueMappings
{
    public static LeagueMemberDto ToDto(this LeagueMember m) => new()
    {
        Id = m.Id,
        UserId = m.UserId,
        Role = m.Role.ToString(),
        DisplayName = m.DisplayName,
        AvatarUrl = m.AvatarUrl,
        CampaignId = m.CampaignId,
        CandidateName = m.Candidate?.Name,
        CandidateSlug = m.Candidate?.Slug,
        Party = m.Candidate?.Party,
        JoinedAt = DateTime.SpecifyKind(m.JoinedAt, DateTimeKind.Utc),
    };

    /// <summary>
    /// Invite → DTO. <paramref name="accepted"/> is supplied by the caller for personal email invites
    /// (true once the recipient has joined); it can't be derived from the invite row alone.
    /// </summary>
    public static LeagueInviteDto ToDto(this LeagueInvite i, DateTime now, bool accepted = false) => new()
    {
        Id = i.Id,
        Code = i.Code,
        JoinPath = $"/leagues/join/{i.Code}",
        Email = i.Email,
        Accepted = accepted,
        ExpiresAt = i.ExpiresAt,
        MaxUses = i.MaxUses,
        UseCount = i.UseCount,
        IsValid = i.IsValid(now),
        CreatedAt = DateTime.SpecifyKind(i.CreatedAt, DateTimeKind.Utc),
    };

    /// <summary>
    /// Round → summary. The caller supplies the requesting user id (to flag "I entered") and an
    /// optional winner display name (resolved from the league's members).
    /// </summary>
    public static LeagueRoundSummaryDto ToSummaryDto(this LeagueRound r, string userId, string? winnerName = null) => new()
    {
        Id = r.Id,
        RoundNumber = r.RoundNumber,
        Status = r.Status.ToString(),
        BriefingSlug = r.BriefingSlug,
        Headline = r.Headline,
        EntryCount = r.Entries.Count,
        IHaveEntered = r.Entries.Any(e => e.UserId == userId),
        WinnerMemberId = r.WinnerMemberId,
        WinnerDisplayName = winnerName,
        ResponsesCloseAt = r.ResponsesCloseAt,
        VotingCloseAt = r.VotingCloseAt,
        CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
    };
}
