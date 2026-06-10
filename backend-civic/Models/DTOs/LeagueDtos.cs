namespace Civic.API.Models.DTOs;

// ---------------------------------------------------------------- Requests

public class CreateLeagueRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Snapshotted onto the owner's membership (no civic User table to read from).</summary>
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class CreateInviteRequest
{
    public DateTime? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
}

/// <summary>Invite one or more friends by email. Each address gets its own single-use personal invite.</summary>
public class InviteByEmailRequest
{
    public List<string> Emails { get; set; } = new();
}

public class JoinLeagueRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class RefreshIdentityRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class LinkCampaignRequest
{
    public Guid CampaignId { get; set; }
}

public class OpenRoundRequest
{
    public string BriefingSlug { get; set; } = "";
    public DateTime? ResponsesCloseAt { get; set; }
    public DateTime? VotingCloseAt { get; set; }
}

public class SubmitRoundEntryRequest
{
    public string OptionId { get; set; } = "";
    public string? Tone { get; set; }
}

// ---------------------------------------------------------------- League

public class LeagueSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int SeasonNumber { get; set; }
    public int MemberCount { get; set; }
    public int MaxMembers { get; set; }
    public string MyRole { get; set; } = "";
    public bool HasLinkedCampaign { get; set; }
    public Guid? ActiveRoundId { get; set; }
    public string? ActiveRoundStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LeagueMemberDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CandidateName { get; set; }
    public string? CandidateSlug { get; set; }
    public string? Party { get; set; }
    public DateTime JoinedAt { get; set; }
}

/// <summary>One row of the season standings: round points + campaign performance combined.</summary>
public class LeagueStandingDto
{
    public Guid MemberId { get; set; }
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? CandidateName { get; set; }
    public string? Party { get; set; }
    public int Rank { get; set; }
    public int LeagueScore { get; set; }
    public int RoundPoints { get; set; }
    public int CampaignScore { get; set; }
    /// <summary>Linked candidate's current support share (0..100), if a campaign is linked.</summary>
    public double? SupportShare { get; set; }
    public bool? Won { get; set; }
    public bool IsMe { get; set; }
}

public class LeagueDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string OwnerUserId { get; set; } = "";
    public int SeasonNumber { get; set; }
    public int MaxMembers { get; set; }
    public string MyRole { get; set; } = "";
    public Guid MyMemberId { get; set; }
    public Guid? MyCampaignId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<LeagueMemberDto> Members { get; set; } = new();
    public List<LeagueStandingDto> Standings { get; set; } = new();
    public LeagueRoundSummaryDto? ActiveRound { get; set; }
    public List<LeagueRoundSummaryDto> Rounds { get; set; } = new();
}

// ---------------------------------------------------------------- Invites

public class LeagueInviteDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    /// <summary>App-relative path the FE turns into a full URL, e.g. /leagues/join/ABCD1234.</summary>
    public string JoinPath { get; set; } = "";
    /// <summary>Set when this is a personal email invite; null for an open share link.</summary>
    public string? Email { get; set; }
    /// <summary>True when a personal invite's recipient has already joined the league.</summary>
    public bool Accepted { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int UseCount { get; set; }
    public bool IsValid { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Per-address outcome of an "invite by email" batch, so the UI can report each one.</summary>
public class EmailInviteResultDto
{
    public string Email { get; set; } = "";
    /// <summary>invited | already_member | already_invited | invalid</summary>
    public string Status { get; set; } = "";
    /// <summary>The invite to share (for invited/already_invited); null for member/invalid.</summary>
    public LeagueInviteDto? Invite { get; set; }
}

public class LeagueInvitePreviewDto
{
    public string Code { get; set; } = "";
    public string LeagueName { get; set; } = "";
    public int MemberCount { get; set; }
    public int MaxMembers { get; set; }
    // Who the invite is from, so the recipient knows who they're joining.
    public string? InviterDisplayName { get; set; }
    public string? InviterEmail { get; set; }
    public string? InviterAvatarUrl { get; set; }
    public bool IsValid { get; set; }
    /// <summary>Why the invite can't be used (expired/revoked/full), when IsValid is false.</summary>
    public string? Reason { get; set; }
    public bool AlreadyMember { get; set; }
    public bool IsFull { get; set; }
}

// ---------------------------------------------------------------- Rounds

public class LeagueRoundSummaryDto
{
    public Guid Id { get; set; }
    public int RoundNumber { get; set; }
    public string Status { get; set; } = "";
    public string BriefingSlug { get; set; } = "";
    public string Headline { get; set; } = "";
    public int EntryCount { get; set; }
    public bool IHaveEntered { get; set; }
    public Guid? WinnerMemberId { get; set; }
    public string? WinnerDisplayName { get; set; }
    public DateTime? ResponsesCloseAt { get; set; }
    public DateTime? VotingCloseAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeagueRoundEntryDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public bool IsMe { get; set; }
    public string? OptionLabel { get; set; }
    public int PointsEarned { get; set; }
    /// <summary>Whole-post net (up - down); the ranking key when the round is closed.</summary>
    public int Net { get; set; }
    public bool IsWinner { get; set; }
    public CampaignPostDto Post { get; set; } = new();
}

public class LeagueRoundDetailDto
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public int RoundNumber { get; set; }
    public string Status { get; set; } = "";
    public string MyRole { get; set; } = "";

    // The shared scenario.
    public string BriefingSlug { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> ValuesInConflict { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public DateTime? ResponsesCloseAt { get; set; }
    public DateTime? VotingCloseAt { get; set; }

    // My submission state (only while OpenForResponses + I have a linked candidate).
    public bool IHaveEntered { get; set; }
    public bool CanSubmit { get; set; }
    public string? CannotSubmitReason { get; set; }
    public List<NewsResponseOptionDetailDto> Options { get; set; } = new();

    // Entries feed (visible once I've entered or once voting/closed).
    public bool EntriesVisible { get; set; }
    public List<LeagueRoundEntryDto> Entries { get; set; } = new();

    public Guid? WinnerMemberId { get; set; }
    public string? WinnerDisplayName { get; set; }
}

public class LeagueRoundResultsDto
{
    public Guid Id { get; set; }
    public int RoundNumber { get; set; }
    public string Headline { get; set; } = "";
    public Guid? WinnerMemberId { get; set; }
    public string? WinnerDisplayName { get; set; }
    public List<LeagueRoundEntryDto> Entries { get; set; } = new();
}
