using System.ComponentModel.DataAnnotations;

namespace Civic.API.Models;

public enum LeagueMemberRole
{
    /// <summary>Created the league; can invite, open/close rounds, and manage settings.</summary>
    Owner,
    /// <summary>Joined via an invite; competes but can't run the league.</summary>
    Member,
}

public enum LeagueRoundStatus
{
    /// <summary>Members may submit their candidate's response to the shared scenario.</summary>
    OpenForResponses,
    /// <summary>Submissions are locked; members vote on each other's responses.</summary>
    Voting,
    /// <summary>Voting is over; points are awarded and a winner is set.</summary>
    Closed,
}

/// <summary>
/// A social competition group. The owner invites friends (via a shareable code), and members
/// compete two ways: an individual leaderboard driven by each member's ongoing Campaign Manager
/// campaign, and shared head-to-head <see cref="LeagueRound"/>s where everyone responds to the same
/// news scenario and votes on each other's responses. Both feed one season standings table.
/// </summary>
public class League
{
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = "";

    [MaxLength(400)]
    public string? Description { get; set; }

    /// <summary>Civic identifies users by a string id (JWT sub). The owner also has a Member row.</summary>
    [Required, MaxLength(120)]
    public string OwnerUserId { get; set; } = "";

    /// <summary>The active season. Bumped on rollover (deferred); members' points reset per season.</summary>
    public int SeasonNumber { get; set; } = 1;

    /// <summary>Hard cap on members (owner included). Enforced at join time.</summary>
    public int MaxMembers { get; set; } = 20;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<LeagueMember> Members { get; set; } = new();
    public List<LeagueRound> Rounds { get; set; } = new();
    public List<LeagueInvite> Invites { get; set; } = new();
}

/// <summary>
/// One user's membership in a league. Because Civic has no User table, the member's display name and
/// avatar are snapshotted from the auth profile at join (refreshable). The member links one of their
/// existing <see cref="CivicCampaign"/>s; that campaign's candidate is the one they field in rounds.
/// </summary>
public class LeagueMember
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public LeagueMemberRole Role { get; set; } = LeagueMemberRole.Member;

    /// <summary>Snapshot of the user's display name at join (no civic User table to join against).</summary>
    [MaxLength(160)]
    public string DisplayName { get; set; } = "";

    /// <summary>Snapshot of the user's email at join, so an invite can credit who it's from.</summary>
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    /// <summary>The Campaign Manager campaign the member competes with. Null until they link one.</summary>
    public Guid? CampaignId { get; set; }
    public CivicCampaign? Campaign { get; set; }

    /// <summary>The candidate the member fields, derived from the linked campaign. Null until linked.</summary>
    public Guid? CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    /// <summary>Denormalized cache of round points this season; recomputable from entries.</summary>
    public int SeasonPoints { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime IdentityRefreshedAt { get; set; } = DateTime.UtcNow;

    public List<LeagueRoundEntry> Entries { get; set; } = new();
}

/// <summary>
/// A shareable invite to a league. The owner generates one or more; each carries a short URL-safe
/// code, optional expiry, and optional use cap. <see cref="IsValid"/> is computed, never stored.
/// </summary>
public class LeagueInvite
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    /// <summary>Short, URL-safe, uppercased, globally unique join code.</summary>
    [Required, MaxLength(16)]
    public string Code { get; set; } = "";

    [Required, MaxLength(120)]
    public string CreatedByUserId { get; set; } = "";

    /// <summary>Null = never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Null = unlimited uses.</summary>
    public int? MaxUses { get; set; }

    public int UseCount { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsValid(DateTime now) =>
        !IsRevoked
        && (ExpiresAt is null || ExpiresAt > now)
        && (MaxUses is null || UseCount < MaxUses);
}

/// <summary>
/// A shared head-to-head round: every member responds to the SAME news <see cref="Briefing"/> with
/// their candidate, then members vote on each other's responses. The owner drives the lifecycle
/// (open → voting → closed). On close, points are awarded and a winner is recorded.
/// </summary>
public class LeagueRound
{
    public Guid Id { get; set; }

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public int SeasonNumber { get; set; } = 1;

    /// <summary>1-based round number within the league + season.</summary>
    public int RoundNumber { get; set; }

    /// <summary>The briefing the round is built around.</summary>
    [Required, MaxLength(160)]
    public string BriefingSlug { get; set; } = "";

    /// <summary>Snapshot of the briefing headline (briefings can change/rotate).</summary>
    [MaxLength(300)]
    public string Headline { get; set; } = "";

    public LeagueRoundStatus Status { get; set; } = LeagueRoundStatus.OpenForResponses;

    public DateTime OpensAt { get; set; } = DateTime.UtcNow;

    /// <summary>Advisory UI countdown only — not scheduler-enforced in MVP.</summary>
    public DateTime? ResponsesCloseAt { get; set; }
    public DateTime? VotingCloseAt { get; set; }

    /// <summary>Set on close. Null on a tie with no entries.</summary>
    public Guid? WinnerMemberId { get; set; }
    public LeagueMember? WinnerMember { get; set; }

    /// <summary>JSON audit written at close: { memberId: pointsAwarded }.</summary>
    public string PointsAwardedJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<LeagueRoundEntry> Entries { get; set; } = new();
}

/// <summary>
/// A single member's response in a round. It reuses the existing <see cref="CampaignPost"/> +
/// reaction machinery for the body and voting — the entry just records who submitted what and the
/// points they earned when the round closed.
/// </summary>
public class LeagueRoundEntry
{
    public Guid Id { get; set; }

    public Guid LeagueRoundId { get; set; }
    public LeagueRound? Round { get; set; }

    public Guid LeagueMemberId { get; set; }
    public LeagueMember? Member { get; set; }

    /// <summary>Denormalized from the member for cheap own-entry self-vote checks.</summary>
    [Required, MaxLength(120)]
    public string UserId { get; set; } = "";

    public Guid CandidateId { get; set; }
    public VirtualCandidate? Candidate { get; set; }

    /// <summary>The generated post that holds the body, fragments, and up/down counts.</summary>
    public Guid PostId { get; set; }
    public CampaignPost? Post { get; set; }

    [MaxLength(20)]
    public string? OptionId { get; set; }

    [MaxLength(120)]
    public string? OptionLabel { get; set; }

    public CampaignTone? Tone { get; set; }

    /// <summary>Set when the round closes.</summary>
    public int PointsEarned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
