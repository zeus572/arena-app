using System.Security.Cryptography;
using Civic.API.Data;
using Civic.API.Mapping;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Leagues;

/// <summary>
/// Scoped orchestrator for the social layer: leagues, membership, and shareable invites. Round
/// mechanics live in <see cref="LeagueRoundService"/>; standings math lives in
/// <see cref="LeagueScoringService"/>. Every league-scoped call resolves the caller's membership and
/// treats a non-member exactly like a missing league (404) so league existence never leaks.
/// </summary>
public class LeagueService
{
    private readonly CivicDbContext _db;
    private readonly LeagueScoringService _scoring;

    // URL-safe, unambiguous code alphabet (no 0/O/1/I).
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    public LeagueService(CivicDbContext db, LeagueScoringService scoring)
    {
        _db = db;
        _scoring = scoring;
    }

    // ---------------------------------------------------------------- Create

    public async Task<LeagueDetailDto> CreateAsync(string userId, CreateLeagueRequest req, CancellationToken ct = default)
    {
        var name = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new LeagueValidationException("Give your league a name.");
        if (name.Length > 120)
            throw new LeagueValidationException("League name is too long (max 120 characters).");

        // Names are unique per organizer (case-insensitive), so a user can't run two leagues with
        // the same name. Different owners may reuse a name freely.
        var lower = name.ToLowerInvariant();
        if (await _db.Leagues.AnyAsync(l => l.OwnerUserId == userId && l.Name.ToLower() == lower, ct))
            throw new LeagueConflictException($"You already have a league named \"{name}\".");

        var league = new League
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description!.Trim(),
            OwnerUserId = userId,
        };
        league.Members.Add(new LeagueMember
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            UserId = userId,
            Role = LeagueMemberRole.Owner,
            DisplayName = ResolveDisplayName(req.DisplayName),
            Email = ResolveEmail(req.Email),
            AvatarUrl = req.AvatarUrl,
        });
        _db.Leagues.Add(league);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Backstop for a race on the unique (OwnerUserId, Name) index.
            throw new LeagueConflictException($"You already have a league named \"{name}\".");
        }

        return await GetDetailAsync(userId, league.Id, ct);
    }

    // ---------------------------------------------------------------- List / detail

    public async Task<List<LeagueSummaryDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        var leagues = await _db.Leagues
            .Where(l => l.Members.Any(m => m.UserId == userId))
            .Include(l => l.Members)
            .Include(l => l.Rounds)
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync(ct);

        return leagues.Select(l =>
        {
            var me = l.Members.First(m => m.UserId == userId);
            var active = l.Rounds
                .Where(r => r.Status != LeagueRoundStatus.Closed)
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefault();
            return new LeagueSummaryDto
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                SeasonNumber = l.SeasonNumber,
                MemberCount = l.Members.Count,
                MaxMembers = l.MaxMembers,
                MyRole = me.Role.ToString(),
                HasLinkedCampaign = me.CampaignId is not null,
                ActiveRoundId = active?.Id,
                ActiveRoundStatus = active?.Status.ToString(),
                CreatedAt = DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(l.UpdatedAt, DateTimeKind.Utc),
            };
        }).ToList();
    }

    public async Task<LeagueDetailDto> GetDetailAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);
        return await BuildDetailAsync(league, me, ct);
    }

    // ---------------------------------------------------------------- Invites

    public async Task<LeagueInviteDto> CreateInviteAsync(string userId, Guid leagueId, CreateInviteRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireOwner(league, userId);

        // An open share link is meant to be reusable; a cap of 1 would make it single-use
        // (use the email-invite flow for that). Null means unlimited.
        if (req.MaxUses is int max && max < 2)
            throw new LeagueValidationException("Max uses must be at least 2 (leave unset for unlimited).");

        var invite = new LeagueInvite
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            CreatedByUserId = userId,
            ExpiresAt = req.ExpiresAt,
            MaxUses = req.MaxUses,
        };
        await SaveInviteWithUniqueCodeAsync(invite, ct);

        return invite.ToDto(DateTime.UtcNow);
    }

    /// <summary>Assigns a unique code and persists the invite, tolerating a collision on the unique index.</summary>
    private async Task SaveInviteWithUniqueCodeAsync(LeagueInvite invite, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            invite.Code = GenerateCode();
            _db.LeagueInvites.Add(invite);
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 5)
            {
                _db.Entry(invite).State = EntityState.Detached;
            }
        }
    }

    public async Task<List<LeagueInviteDto>> ListInvitesAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireOwner(league, userId);

        var now = DateTime.UtcNow;
        var invites = await _db.LeagueInvites
            .Where(i => i.LeagueId == leagueId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        // A personal invite counts as "accepted" once a member with that email exists.
        var memberEmails = league.Members
            .Where(m => m.Email is not null)
            .Select(m => m.Email!.ToLowerInvariant())
            .ToHashSet();

        return invites
            .Select(i => i.ToDto(now, accepted: i.Email is not null && memberEmails.Contains(i.Email)))
            .ToList();
    }

    /// <summary>
    /// Invite friends by email. Each address becomes its own single-use personal invite. Addresses
    /// already in the league are skipped (already_member), and re-inviting a pending address returns
    /// the existing invite (already_invited) rather than piling up duplicates.
    /// </summary>
    public async Task<List<EmailInviteResultDto>> CreateEmailInvitesAsync(string userId, Guid leagueId, InviteByEmailRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireOwner(league, userId);

        // Normalize, validate, and de-duplicate the requested addresses (order-preserving).
        var seen = new HashSet<string>();
        var requested = new List<(string raw, string? normalized)>();
        foreach (var raw in req.Emails ?? new List<string>())
        {
            var trimmed = (raw ?? "").Trim();
            if (trimmed.Length == 0) continue;
            var normalized = NormalizeEmail(trimmed);
            if (normalized is not null && !seen.Add(normalized)) continue; // drop dup within the batch
            requested.Add((trimmed, normalized));
        }

        var now = DateTime.UtcNow;
        var memberEmails = league.Members
            .Where(m => m.Email is not null)
            .Select(m => m.Email!.ToLowerInvariant())
            .ToHashSet();
        var existing = await _db.LeagueInvites
            .Where(i => i.LeagueId == leagueId && i.Email != null && !i.IsRevoked)
            .ToListAsync(ct);
        var existingByEmail = existing
            .Where(i => i.Email is not null)
            .GroupBy(i => i.Email!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.CreatedAt).First());

        var results = new List<EmailInviteResultDto>();
        foreach (var (raw, normalized) in requested)
        {
            if (normalized is null)
            {
                results.Add(new EmailInviteResultDto { Email = raw, Status = "invalid" });
                continue;
            }
            if (memberEmails.Contains(normalized))
            {
                results.Add(new EmailInviteResultDto { Email = normalized, Status = "already_member" });
                continue;
            }
            if (existingByEmail.TryGetValue(normalized, out var prior) && prior.IsValid(now))
            {
                results.Add(new EmailInviteResultDto { Email = normalized, Status = "already_invited", Invite = prior.ToDto(now) });
                continue;
            }

            var invite = new LeagueInvite
            {
                Id = Guid.NewGuid(),
                LeagueId = league.Id,
                CreatedByUserId = userId,
                Email = normalized,
                MaxUses = 1, // a personal invite is meant for one friend
            };
            await SaveInviteWithUniqueCodeAsync(invite, ct);
            existingByEmail[normalized] = invite;
            results.Add(new EmailInviteResultDto { Email = normalized, Status = "invited", Invite = invite.ToDto(now) });
        }

        return results;
    }

    public async Task RevokeInviteAsync(string userId, Guid leagueId, Guid inviteId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        RequireOwner(league, userId);

        var invite = await _db.LeagueInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.LeagueId == leagueId, ct)
            ?? throw new LeagueNotFoundException("Invite not found.");
        invite.IsRevoked = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<LeagueInvitePreviewDto> PreviewInviteAsync(string userId, string code, CancellationToken ct = default)
    {
        var (invite, league) = await LoadInviteAsync(code, ct);
        var now = DateTime.UtcNow;
        var alreadyMember = league.Members.Any(m => m.UserId == userId);
        var isFull = league.Members.Count >= league.MaxMembers;
        var valid = invite.IsValid(now);

        // Who the invite is from: the member who created it, falling back to the owner if that
        // member has since left.
        var inviter = league.Members.FirstOrDefault(m => m.UserId == invite.CreatedByUserId)
            ?? league.Members.FirstOrDefault(m => m.Role == LeagueMemberRole.Owner);

        return new LeagueInvitePreviewDto
        {
            Code = invite.Code,
            LeagueName = league.Name,
            MemberCount = league.Members.Count,
            MaxMembers = league.MaxMembers,
            InviterDisplayName = inviter?.DisplayName,
            InviterEmail = inviter?.Email,
            InviterAvatarUrl = inviter?.AvatarUrl,
            IsValid = valid,
            Reason = valid ? null : InvalidReason(invite, now),
            AlreadyMember = alreadyMember,
            IsFull = isFull,
        };
    }

    /// <summary>
    /// A privacy-safe preview for signed-out visitors landing on a join link. Unlike
    /// <see cref="PreviewInviteAsync"/> this needs no caller identity, so it can power an enticing
    /// "12 members, organized by Ada — sign in to join" card before the visitor has an account. It
    /// surfaces only the league name, headcount, and organizer; never member emails.
    /// </summary>
    public async Task<LeagueInvitePublicPreviewDto> PublicPreviewInviteAsync(string code, CancellationToken ct = default)
    {
        var (invite, league) = await LoadInviteAsync(code, ct);
        var now = DateTime.UtcNow;
        var valid = invite.IsValid(now);
        var organizer = league.Members.FirstOrDefault(m => m.Role == LeagueMemberRole.Owner);

        return new LeagueInvitePublicPreviewDto
        {
            Code = invite.Code,
            LeagueName = league.Name,
            MemberCount = league.Members.Count,
            MaxMembers = league.MaxMembers,
            OrganizerDisplayName = organizer?.DisplayName,
            OrganizerAvatarUrl = organizer?.AvatarUrl,
            IsValid = valid,
            Reason = valid ? null : InvalidReason(invite, now),
            IsFull = league.Members.Count >= league.MaxMembers,
        };
    }

    public async Task<LeagueDetailDto> JoinAsync(string userId, string code, JoinLeagueRequest req, CancellationToken ct = default)
    {
        var (invite, league) = await LoadInviteAsync(code, ct);

        // Already a member: a no-op success — just hand back the league.
        var existing = league.Members.FirstOrDefault(m => m.UserId == userId);
        if (existing is not null)
            return await BuildDetailAsync(league, existing, ct);

        var now = DateTime.UtcNow;
        if (!invite.IsValid(now))
            throw new LeagueInviteGoneException(InvalidReason(invite, now));
        if (league.Members.Count >= league.MaxMembers)
            throw new LeagueConflictException("This league is full.");

        var member = new LeagueMember
        {
            Id = Guid.NewGuid(),
            LeagueId = league.Id,
            UserId = userId,
            Role = LeagueMemberRole.Member,
            DisplayName = ResolveDisplayName(req.DisplayName),
            Email = ResolveEmail(req.Email),
            AvatarUrl = req.AvatarUrl,
        };
        _db.LeagueMembers.Add(member);
        invite.UseCount += 1;
        league.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(userId, league.Id, ct);
    }

    // ---------------------------------------------------------------- Membership management

    public async Task<LeagueDetailDto> LinkCampaignAsync(string userId, Guid leagueId, LinkCampaignRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);

        var campaign = await _db.CivicCampaigns
            .FirstOrDefaultAsync(c => c.Id == req.CampaignId, ct);
        if (campaign is null || campaign.UserId != userId)
            throw new LeagueValidationException("That campaign doesn't exist or isn't yours.");

        me.CampaignId = campaign.Id;
        me.CandidateId = campaign.CandidateId;
        league.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(userId, leagueId, ct);
    }

    public async Task<LeagueDetailDto> RefreshIdentityAsync(string userId, Guid leagueId, RefreshIdentityRequest req, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);

        me.DisplayName = ResolveDisplayName(req.DisplayName);
        me.Email = ResolveEmail(req.Email);
        me.AvatarUrl = req.AvatarUrl;
        me.IdentityRefreshedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDetailAsync(userId, leagueId, ct);
    }

    public async Task LeaveAsync(string userId, Guid leagueId, CancellationToken ct = default)
    {
        var league = await LoadLeagueAsync(leagueId, ct);
        var me = RequireMember(league, userId);
        if (me.Role == LeagueMemberRole.Owner)
            throw new LeagueConflictException("The owner can't leave their own league.");

        _db.LeagueMembers.Remove(me);
        league.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- Build detail

    private async Task<LeagueDetailDto> BuildDetailAsync(League league, LeagueMember me, CancellationToken ct)
    {
        var campaignsById = await LoadLinkedCampaignsAsync(league, ct);
        var standings = _scoring.ComputeStandings(league, campaignsById, me.UserId);

        var memberById = league.Members.ToDictionary(m => m.Id);
        string? WinnerName(LeagueRound r) =>
            r.WinnerMemberId is Guid wid && memberById.TryGetValue(wid, out var w) ? w.DisplayName : null;

        var rounds = league.Rounds
            .OrderByDescending(r => r.RoundNumber)
            .Select(r => r.ToSummaryDto(me.UserId, WinnerName(r)))
            .ToList();
        var active = league.Rounds
            .Where(r => r.Status != LeagueRoundStatus.Closed)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefault();

        return new LeagueDetailDto
        {
            Id = league.Id,
            Name = league.Name,
            Description = league.Description,
            OwnerUserId = league.OwnerUserId,
            SeasonNumber = league.SeasonNumber,
            MaxMembers = league.MaxMembers,
            MyRole = me.Role.ToString(),
            MyMemberId = me.Id,
            MyCampaignId = me.CampaignId,
            CreatedAt = DateTime.SpecifyKind(league.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(league.UpdatedAt, DateTimeKind.Utc),
            Members = league.Members.OrderBy(m => m.JoinedAt).Select(m => m.ToDto()).ToList(),
            Standings = standings,
            ActiveRound = active?.ToSummaryDto(me.UserId, WinnerName(active)),
            Rounds = rounds,
        };
    }

    private async Task<Dictionary<Guid, CivicCampaign>> LoadLinkedCampaignsAsync(League league, CancellationToken ct)
    {
        var ids = league.Members.Where(m => m.CampaignId is not null).Select(m => m.CampaignId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new();
        return await _db.CivicCampaigns
            .Where(c => ids.Contains(c.Id))
            .Include(c => c.Standings)
            .ToDictionaryAsync(c => c.Id, ct);
    }

    // ---------------------------------------------------------------- Internals

    /// <summary>Loads a league with members (incl. their candidate) and rounds (incl. entries).</summary>
    private async Task<League> LoadLeagueAsync(Guid leagueId, CancellationToken ct)
    {
        return await _db.Leagues
            .Include(l => l.Members).ThenInclude(m => m.Candidate)
            .Include(l => l.Rounds).ThenInclude(r => r.Entries)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw new LeagueNotFoundException();
    }

    private async Task<(LeagueInvite invite, League league)> LoadInviteAsync(string code, CancellationToken ct)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        var invite = await _db.LeagueInvites.FirstOrDefaultAsync(i => i.Code == normalized, ct)
            ?? throw new LeagueNotFoundException("Invite not found.");
        var league = await LoadLeagueAsync(invite.LeagueId, ct);
        return (invite, league);
    }

    private static LeagueMember RequireMember(League league, string userId)
        => league.Members.FirstOrDefault(m => m.UserId == userId)
           ?? throw new LeagueNotFoundException();

    private static void RequireOwner(League league, string userId)
    {
        var me = RequireMember(league, userId);
        if (me.Role != LeagueMemberRole.Owner)
            throw new LeagueForbiddenException();
    }

    private static string ResolveDisplayName(string? displayName)
    {
        var name = (displayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return "Member";
        return name.Length > 160 ? name[..160] : name;
    }

    private static string? ResolveEmail(string? email)
    {
        var e = (email ?? "").Trim();
        if (string.IsNullOrWhiteSpace(e)) return null;
        return e.Length > 200 ? e[..200] : e;
    }

    /// <summary>Validates and lowercases an email for use as an invite key. Returns null if it isn't a plausible address.</summary>
    private static string? NormalizeEmail(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        if (e.Length is 0 or > 200) return null;
        // Lightweight shape check: exactly one @, non-empty local part, and a dotted domain.
        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return null;
        var domain = e[(at + 1)..];
        if (domain.Length < 3 || !domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.')) return null;
        return e;
    }

    private static string InvalidReason(LeagueInvite invite, DateTime now)
    {
        if (invite.IsRevoked) return "This invite was revoked.";
        if (invite.ExpiresAt is not null && invite.ExpiresAt <= now) return "This invite has expired.";
        if (invite.MaxUses is not null && invite.UseCount >= invite.MaxUses) return "This invite has reached its use limit.";
        return "This invite is no longer valid.";
    }

    private static string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[CodeLength];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        return new string(chars);
    }
}
