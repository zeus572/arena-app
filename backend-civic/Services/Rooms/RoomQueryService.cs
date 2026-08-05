using System.Text.Json;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>
/// The single public read path for rooms.
///
/// Everything anonymous goes through here so the visibility rules — unpublished rooms are
/// invisible, the locality read-wall, and later the six-hour unreviewed-flag rule — live in
/// one place instead of being re-derived per controller action.
/// </summary>
public class RoomQueryService
{
    private readonly CivicDbContext _db;

    public RoomQueryService(CivicDbContext db) => _db = db;

    /// <summary>
    /// Rooms a reader is allowed to see. Draft and in-review rooms never leave the admin API.
    /// Locality follows the same wall as briefings: a state-scoped room is visible to that
    /// state and to nobody else; null locality is national and visible to everyone.
    /// </summary>
    public IQueryable<Room> VisibleRooms(string? viewerLocality)
    {
        var q = _db.Rooms.AsNoTracking().Where(r =>
            r.Status == RoomStatus.Published ||
            r.Status == RoomStatus.Monitoring ||
            r.Status == RoomStatus.CorrectionRequired);

        return q.Where(r => r.Locality == null || r.Locality == viewerLocality);
    }

    public async Task<List<RoomSummaryDto>> ListAsync(
        string? viewerLocality,
        string? kind = null,
        int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);

        var q = VisibleRooms(viewerLocality);

        q = kind?.ToLowerInvariant() switch
        {
            "theme" => q.OfType<ThemeRoom>(),
            "story" => q.OfType<StoryRoom>(),
            _ => q,
        };

        var rooms = await q
            .OrderByDescending(r => r.LastMeaningfulUpdateAt ?? r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return rooms.Select(ToSummary).ToList();
    }

    public Task<Room?> FindBySlugAsync(
        string slug, string? viewerLocality, CancellationToken ct = default)
        => VisibleRooms(viewerLocality).FirstOrDefaultAsync(r => r.Slug == slug, ct)!;

    /// <summary>
    /// This reader's state for a room, plus their delta when they have seen it before.
    ///
    /// A first-time reader gets no delta at all — showing "0 changes since your last visit"
    /// to someone who has never visited is noise, and the front door already says everything
    /// a first visit needs.
    /// </summary>
    public async Task<RoomViewerStateDto> ViewerStateAsync(
        string userId, Room room, RoomRevisionService revisions, CancellationToken ct = default)
    {
        var state = await _db.UserRoomStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.RoomId == room.Id, ct);

        var density = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (RoomDensity?)p.RoomDensity)
            .FirstOrDefaultAsync(ct) ?? RoomDensity.Read;

        var dto = new RoomViewerStateDto
        {
            LastSeenRevision = state?.LastSeenRevision ?? 0,
            Following = state?.Following ?? false,
            Density = density.ToString(),
            SectionProgress = state?.SectionProgress.Select(p => new SectionProgressDto
            {
                SectionKey = p.SectionKey,
                Opened = p.Opened,
                ItemsSeen = p.ItemsSeen,
                ItemsTotal = p.ItemsTotal,
            }).ToList() ?? new List<SectionProgressDto>(),
        };

        if (state is not null && state.LastSeenRevision > 0 && state.LastSeenRevision < room.Revision)
        {
            dto.Delta = await revisions.DeltaAsync(room.Id, state.LastSeenRevision, ct);
        }

        return dto;
    }

    /// <summary>
    /// The Theme Room front door. Essential-fact statuses are read from the CLAIMS, never
    /// from a cached copy on the room — that is what makes a correction reach the front door
    /// without anyone editing this room.
    /// </summary>
    public async Task<ThemeRoomDetailDto> ToThemeDetailAsync(
        ThemeRoom room, CancellationToken ct = default)
    {
        var claimIds = room.EssentialFacts
            .Where(f => f.ClaimId is not null)
            .Select(f => f.ClaimId!.Value)
            .ToList();

        var claims = claimIds.Count == 0
            ? new Dictionary<Guid, (string Slug, ClaimStatus Status)>()
            : await _db.Claims.AsNoTracking()
                .Where(c => claimIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Slug, c.Status })
                .ToDictionaryAsync(c => c.Id, c => (c.Slug, c.Status), ct);

        var dto = new ThemeRoomDetailDto
        {
            AlternateTitles = room.AlternateTitles,
            ScopeStatement = room.ScopeStatement,
            InclusionRules = room.InclusionRules,
            ExclusionRules = room.ExclusionRules,
            CurrentStatusSentence = room.CurrentStatusSentence,
            TopUnresolvedQuestion = room.TopUnresolvedQuestion,
            WatchNext = room.WatchNext,
            MonitoringCadence = room.MonitoringCadence.ToString(),
            ArticlesConsideredCount = room.ArticlesConsideredCount,
            DevelopmentWindowDays = room.DevelopmentWindowDays,
            EssentialFacts = room.EssentialFacts
                .OrderBy(f => f.Ordinal)
                .Select(f => new EssentialFactDto
                {
                    Text = f.Text,
                    ClaimId = f.ClaimId,
                    ClaimSlug = f.ClaimId is { } id && claims.TryGetValue(id, out var c) ? c.Slug : null,
                    ClaimStatus = f.ClaimId is { } id2 && claims.TryGetValue(id2, out var c2)
                        ? c2.Status.ToString() : null,
                    Ordinal = f.Ordinal,
                }).ToList(),
            TerminologyNotes = room.TerminologyNotes
                .Select(n => new TerminologyNoteDto { Term = n.Term, Note = n.Note })
                .ToList(),
        };

        CopySummary(room, dto);
        return dto;
    }

    public StoryRoomDetailDto ToStoryDetail(StoryRoom room)
    {
        var dto = new StoryRoomDetailDto
        {
            HowItWorksIntro = room.HowItWorksIntro,
            SourceBillId = room.SourceBillId,
            WhyItMatters = room.WhyItMatters.Select(d => new StoryDimensionDto
            {
                Dimension = d.Dimension,
                Text = d.Text,
                ClaimId = d.ClaimId,
            }).ToList(),
            Stakeholders = room.Stakeholders.Select(s => new StakeholderImpactDto
            {
                Group = s.Group,
                ImpactSummary = s.ImpactSummary,
                Confidence = s.Confidence,
            }).ToList(),
            NextSteps = room.NextSteps.Select(n => new NextStepDto
            {
                Description = n.Description,
                VerificationCondition = n.VerificationCondition,
                ActorId = n.ActorId,
                ExpectedTiming = n.ExpectedTiming,
                PredictionId = n.PredictionId,
            }).ToList(),
            TypePayload = ParsePayload(room.TypePayloadJson),
        };

        CopySummary(room, dto);
        return dto;
    }

    private static JsonElement? ParsePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            // A malformed payload must not take down the whole story page; the admin
            // integrity report is where bad payloads get surfaced.
            return null;
        }
    }

    public static RoomSummaryDto ToSummary(Room room)
    {
        var dto = new RoomSummaryDto();
        CopySummary(room, dto);
        return dto;
    }

    private static void CopySummary(Room room, RoomSummaryDto dto)
    {
        dto.Id = room.Id;
        dto.Slug = room.Slug;
        dto.Kind = room is ThemeRoom ? "Theme" : "Story";
        dto.Title = room.Title;
        dto.Dek = room.Dek;
        dto.Status = room.Status.ToString();
        dto.Locality = room.Locality;
        dto.Revision = room.Revision;
        dto.LastMeaningfulUpdateAt = room.LastMeaningfulUpdateAt;
        dto.ContentNote = room.ContentNote;

        if (room is StoryRoom story)
        {
            dto.StoryType = story.StoryType.ToString();
            dto.EventTime = story.EventTime;
            dto.EstimatedMinutes = story.EstimatedMinutes;
        }
    }
}
