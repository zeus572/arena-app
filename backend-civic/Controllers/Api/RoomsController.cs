using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.DTOs;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Theme and Story Rooms (docs/Rooms Expansion, PRD 01 and PRD 02).
///
/// Reads are anonymous — a room's whole purpose is to be the thing you can land on from a
/// search result and understand without an account. Personalization (delta, following,
/// section progress) layers on top of whichever user id ICurrentUserService resolves,
/// including the literal "anonymous".
///
/// ONE payload serves every density. Design 1c's invariant is that density changes the
/// amount of scaffolding around facts and never the facts themselves; serving the same
/// object graph to Read, Brief and Board is how that becomes structurally true rather than
/// something a reviewer has to keep checking.
///
/// NOTE the deliberate absence of a class-level [AllowAnonymous]. In ASP.NET Core an
/// IAllowAnonymous on the endpoint short-circuits authorization entirely, so a class-level
/// one would silently neuter the method-level [Authorize(Policy = "VerifiedEmail")] on the
/// writes below — leaving follow and section-progress open to anyone. With no class-level
/// attribute and no fallback policy configured, unattributed actions are already anonymous.
/// This matches PetitionsController and CoalitionProvisionsController, which mix the same way.
/// </summary>
[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly RoomQueryService _rooms;
    private readonly RoomRevisionService _revisions;
    private readonly ObjectLinkService _links;

    public RoomsController(
        CivicDbContext db,
        ICurrentUserService user,
        RoomQueryService rooms,
        RoomRevisionService revisions,
        ObjectLinkService links)
    {
        _db = db;
        _user = user;
        _rooms = rooms;
        _revisions = revisions;
        _links = links;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoomSummaryDto>>> List(
        [FromQuery] string? kind, [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _rooms.ListAsync(await ViewerLocalityAsync(ct), kind, take, ct));

    /// <summary>The front door. Theme and story rooms return different shapes.</summary>
    [HttpGet("{slug}")]
    public async Task<ActionResult<object>> GetBySlug(string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var viewer = await _rooms.ViewerStateAsync(_user.GetCurrentUserId(), room, _revisions, ct);

        switch (room)
        {
            case ThemeRoom theme:
            {
                var dto = await _rooms.ToThemeDetailAsync(theme, ct);
                dto.Viewer = viewer;

                // The six-hour rule, at sentence granularity. A room whose status sentence
                // is flagged for rewrite serves everything else and withholds the sentence,
                // rather than vanishing — the timeline, the actors and the claim ledger are
                // still true, and blanking them would destroy more than it protects.
                var hidden = await _rooms.HiddenObjectsAsync(
                    theme.Id, _user.GetCurrentUserId(), DateTime.UtcNow, ct);

                if (hidden.Contains(new ObjectRef(ObjectType.Room, theme.Id)))
                {
                    dto.CurrentStatusSentence = "";
                    dto.StatusSentenceUnderReview = true;
                }

                return Ok(dto);
            }
            case StoryRoom story:
            {
                var dto = await _rooms.ToStoryDetailAsync(story, ct);
                dto.Viewer = viewer;
                return Ok(dto);
            }
            default:
                return NotFound();
        }
    }

    /// <summary>
    /// What changed since <paramref name="sinceRevision"/>, or since this reader's last visit
    /// when the parameter is omitted. Corrections come back in their own array.
    /// </summary>
    [HttpGet("{slug}/delta")]
    public async Task<ActionResult<RoomDeltaDto>> Delta(
        string slug, [FromQuery] int? sinceRevision, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var since = sinceRevision ?? await _db.UserRoomStates.AsNoTracking()
            .Where(s => s.UserId == _user.GetCurrentUserId() && s.RoomId == room.Id)
            .Select(s => s.LastSeenRevision)
            .FirstOrDefaultAsync(ct);

        return Ok(await _revisions.DeltaAsync(room.Id, since, ct));
    }

    /// <summary>
    /// The full changelog, meaningful entries and withheld edits alike.
    ///
    /// The minor edits are served rather than hidden: design 1d's "11 edits we did not bother
    /// you with" is only an honest claim if the reader can go and look at them.
    /// </summary>
    [HttpGet("{slug}/changelog")]
    public async Task<ActionResult<List<ChangeLogEntryDto>>> ChangeLog(
        string slug, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        take = Math.Clamp(take, 1, 300);

        var entries = await _db.ChangeLogEntries.AsNoTracking()
            .Where(e => e.RoomId == room.Id)
            .OrderByDescending(e => e.RevisionNumber).ThenByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return Ok(entries.Select(RoomRevisionService.ToDto).ToList());
    }

    /// <summary>
    /// Revision marks for the scrubber (design 1e): tall marks for meaningful revisions,
    /// short ones for edits. Snapshots are not included — fetch one by number.
    /// </summary>
    [HttpGet("{slug}/revisions")]
    public async Task<ActionResult<List<RoomRevisionMarkDto>>> Revisions(
        string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var marks = await _db.RoomRevisions.AsNoTracking()
            .Where(r => r.RoomId == room.Id)
            .OrderBy(r => r.Revision)
            .Select(r => new RoomRevisionMarkDto
            {
                Revision = r.Revision,
                IsMeaningful = r.IsMeaningful,
                Summary = r.Summary,
                CreatedAt = r.CreatedAt,
                HasSnapshot = r.SnapshotJson != null,
            })
            .ToListAsync(ct);

        return Ok(marks);
    }

    /// <summary>The room as it stood at a past revision. Only meaningful revisions carry one.</summary>
    [HttpGet("{slug}/revisions/{revision:int}")]
    public async Task<ActionResult<object>> RevisionSnapshot(
        string slug, int revision, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var json = await _db.RoomRevisions.AsNoTracking()
            .Where(r => r.RoomId == room.Id && r.Revision == revision)
            .Select(r => r.SnapshotJson)
            .FirstOrDefaultAsync(ct);

        if (json is null) return NotFound();
        return Content(json, "application/json");
    }

    /// <summary>
    /// The Latest section (design 1g) — bounded developments, plus the honest denominator.
    ///
    /// The count of what was EXCLUDED ships alongside the list rather than being computed
    /// on the client, because "we logged 260 and judged eight" is a disclosure and a
    /// disclosure the client could get wrong is not one.
    /// </summary>
    [HttpGet("{slug}/latest")]
    public async Task<ActionResult<RoomLatestDto>> Latest(string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var hidden = await _rooms.HiddenObjectsAsync(
            room.Id, _user.GetCurrentUserId(), DateTime.UtcNow, ct);

        var developments = (await _db.Developments.AsNoTracking()
                .Where(d => d.RoomId == room.Id)
                .OrderByDescending(d => d.OccurredAt)
                .ToListAsync(ct))
            .Where(d => !hidden.Contains(new ObjectRef(ObjectType.Development, d.Id)))
            .ToList();

        var storySlugs = await _db.Rooms.AsNoTracking()
            .Where(r => developments.Select(d => d.StoryRoomId).Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Slug, ct);

        var theme = room as ThemeRoom;

        return Ok(new RoomLatestDto
        {
            ArticlesConsidered = theme?.ArticlesConsideredCount ?? 0,
            WindowDays = theme?.DevelopmentWindowDays ?? 0,
            InclusionRules = theme?.InclusionRules ?? Array.Empty<string>(),
            ExclusionRules = theme?.ExclusionRules ?? Array.Empty<string>(),
            ExcludedCount = Math.Max(0, (theme?.ArticlesConsideredCount ?? 0) - developments.Count),
            Developments = developments.Select(d => new DevelopmentDto
            {
                Id = d.Id,
                OccurredAt = d.OccurredAt,
                Category = d.Category.ToString(),
                Headline = d.Headline,
                Summary = d.Summary,
                WhyItMatters = d.WhyItMatters,
                InclusionReason = d.InclusionReason,
                EvidenceStatus = d.EvidenceStatus.ToString(),
                StoryRoomId = d.StoryRoomId,
                StorySlug = d.StoryRoomId is { } id && storySlugs.TryGetValue(id, out var s) ? s : null,
            }).ToList(),
        });
    }

    /// <summary>
    /// Everything this room rests on, grouped by source type (design 1l).
    ///
    /// Reached by walking the graph rather than by a stored list: a room references claims,
    /// claims cite sources, actors cite a source for what they say they want. Deriving it
    /// means the section cannot drift out of step with the evidence the page actually shows
    /// — adding a source to a claim adds it here, and only here.
    ///
    /// FullTextHeldCount is reported because it is usually low and that is the point. Civic
    /// stores a headline and an RSS summary for reporting, not the article body, so most
    /// rows here corroborate a claim rather than being something a passage was quoted from.
    /// A methodology section that hid that would be advertising a rigour we do not have.
    /// </summary>
    [HttpGet("{slug}/sources")]
    public async Task<ActionResult<RoomSourcesDto>> Sources(string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var claimIds = (await _links.OutgoingAsync(new ObjectRef(ObjectType.Room, room.Id), ct: ct))
            .Where(l => l.ToType == ObjectType.Claim)
            .Select(l => l.ToId)
            .Distinct()
            .ToList();

        var claimRefs = claimIds.Select(id => new ObjectRef(ObjectType.Claim, id)).ToList();
        var byClaim = await _links.OutgoingManyAsync(claimRefs, ct);

        var sourceIds = byClaim
            .SelectMany(g => g)
            .Where(l => l.ToType == ObjectType.SourceRef
                     && (l.Relation == LinkRelation.SupportedBy
                      || l.Relation == LinkRelation.ContradictedBy))
            .Select(l => l.ToId)
            .ToList();

        // Actors cite a source for their stated wants; design 1i requires it, so those
        // sources belong in the methodology list too.
        var actorSourceIds = await _db.ActorRoomRoles.AsNoTracking()
            .Where(r => r.RoomId == room.Id && r.Actor!.StatedWantsSourceRefId != null)
            .Select(r => r.Actor!.StatedWantsSourceRefId!.Value)
            .ToListAsync(ct);

        var allIds = sourceIds.Concat(actorSourceIds).Distinct().ToList();

        var sources = await _db.SourceRefs.AsNoTracking()
            .Where(s => allIds.Contains(s.Id))
            .OrderByDescending(s => s.IsPrimary)
            .ThenByDescending(s => s.PublishedAt)
            .ToListAsync(ct);

        return Ok(new RoomSourcesDto
        {
            Total = sources.Count,
            FullTextHeldCount = sources.Count(s => s.FullTextAvailable),
            Groups = sources
                .GroupBy(s => s.SourceType)
                .OrderBy(g => g.Key)
                .Select(g => new RoomSourceGroupDto
                {
                    SourceType = g.Key.ToString(),
                    Count = g.Count(),
                    Sources = g.Select(s => new RoomSourceDto
                    {
                        Id = s.Id,
                        Url = s.Url,
                        Title = s.Title,
                        Organization = s.Organization,
                        SourceType = s.SourceType.ToString(),
                        IsPrimary = s.IsPrimary,
                        PublishedAt = s.PublishedAt,
                        Availability = s.Availability.ToString(),
                        FullTextAvailable = s.FullTextAvailable,
                    }).ToList(),
                })
                .ToList(),
        });
    }

    /// <summary>
    /// The Money Trail (PRD 05, designs 1s and 1t).
    ///
    /// Every item returns all five rungs, including the empty ones, because the empty rungs
    /// are usually the story: a headline number that has only been requested looks identical
    /// to one that has been spent unless the page can show four blanks above it.
    ///
    /// There is deliberately no total. Requested plus Appropriated is not a quantity — the
    /// same dollars appear at several rungs as they move — so the endpoint reports totals
    /// per stage and <see cref="MoneyMath.TotalAcrossStages"/> throws if anyone asks for the
    /// other kind. Outlays and modelled economic effects are returned in separate groups for
    /// the same reason.
    /// </summary>
    [HttpGet("{slug}/money")]
    public async Task<ActionResult<RoomMoneyDto>> Money(string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var items = await _db.MoneyItems.AsNoTracking()
            .Where(m => m.RoomId == room.Id)
            .OrderBy(m => m.CurrentStage)
            .ThenByDescending(m => m.AmountUsd)
            .ToListAsync(ct);

        var itemIds = items.Select(m => m.Id).ToList();

        var entries = await _db.MoneyStageEntries.AsNoTracking()
            .Where(e => itemIds.Contains(e.MoneyItemId))
            .ToListAsync(ct);

        var sourceIds = entries.Where(e => e.SourceRefId != null)
            .Select(e => e.SourceRefId!.Value).Distinct().ToList();
        var sources = await _db.SourceRefs.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Title, s.Organization, s.Url }, ct);

        var byItem = entries.ToLookup(e => e.MoneyItemId);

        RoomMoneyItemDto Map(MoneyItem m)
        {
            var rows = byItem[m.Id].OrderBy(e => MoneyMath.Ladder.ToList().IndexOf(e.Stage)).ToList();

            return new RoomMoneyItemDto
            {
                Id = m.Id,
                Slug = m.Slug,
                Title = m.Title,
                Kind = m.Kind.ToString(),
                CategoryKey = m.CategoryKey,
                Jurisdiction = m.Jurisdiction,
                AmountUsd = m.AmountUsd,
                AmountMinUsd = m.AmountMinUsd,
                AmountMaxUsd = m.AmountMaxUsd,
                // Computed here rather than in the client so the "not per year" warning on a
                // multi-year figure cannot be dropped by a caller that forgets it exists.
                PeriodLabel = MoneyMath.PeriodLabel(m),
                IsMultiYear = MoneyMath.IsMultiYear(m),
                CurrentStage = m.CurrentStage.ToString(),
                CurrentStageVerb = MoneyMath.VerbFor(m.CurrentStage),
                CanSaySpent = MoneyMath.CanSaySpent(m.CurrentStage),
                WhatThisDoesNotMean = m.WhatThisDoesNotMean,
                DecidesNext = m.DecidesNext,
                EstimateMethod = m.EstimateMethod,
                Exclusions = m.Exclusions,
                Breakdown = m.Breakdown.Select(b => new RoomMoneyBreakdownDto
                {
                    Label = b.Label,
                    AmountUsd = b.AmountUsd,
                    Note = b.Note,
                }).ToList(),
                Comparisons = m.Comparisons.Select(c => new RoomMoneyComparisonDto
                {
                    Text = c.Text,
                    Accepted = c.Accepted,
                    RejectionReason = c.RejectionReason,
                }).ToList(),
                Stages = rows.Select(e => new RoomMoneyStageDto
                {
                    Stage = e.Stage.ToString(),
                    Verb = MoneyMath.VerbFor(e.Stage),
                    AmountUsd = e.AmountUsd,
                    Applicability = e.Applicability.ToString(),
                    NotApplicableReason = e.NotApplicableReason,
                    AsOf = e.AsOf,
                    SourceTitle = e.SourceRefId is { } sid && sources.TryGetValue(sid, out var s)
                        ? s.Title : null,
                    SourceOrganization = e.SourceRefId is { } sid2 && sources.TryGetValue(sid2, out var s2)
                        ? s2.Organization : null,
                    SourceUrl = e.SourceRefId is { } sid3 && sources.TryGetValue(sid3, out var s3)
                        ? s3.Url : null,
                }).ToList(),
            };
        }

        var outlays = items.Where(m => m.Kind == MoneyItemKind.GovernmentOutlay).ToList();

        return Ok(new RoomMoneyDto
        {
            Ladder = MoneyMath.Ladder.Select(s => s.ToString()).ToList(),
            Items = items.Select(Map).ToList(),
            // Per stage, never across. Named explicitly so a caller cannot drift into
            // "just add up the ladder" — the single most common error in budget coverage.
            TotalsByStage = MoneyMath.Ladder.ToDictionary(
                s => s.ToString(),
                s => MoneyMath.TotalAtStage(
                    entries.Where(e => outlays.Any(o => o.Id == e.MoneyItemId)).ToList(), s)),
            OutlayCount = outlays.Count,
            OtherKindCount = items.Count - outlays.Count,
        });
    }

    /// <summary>The Understand section's timeline (design 1h).</summary>
    [HttpGet("{slug}/timeline")]
    public async Task<ActionResult<List<TimelineEventDto>>> Timeline(string slug, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var events = await _db.TimelineEvents.AsNoTracking()
            .Where(t => t.RoomId == room.Id)
            .OrderBy(t => t.OccurredOn).ThenBy(t => t.Ordinal)
            .ToListAsync(ct);

        return Ok(events.Select(t => new TimelineEventDto
        {
            OccurredOn = t.OccurredOn,
            OccurredPrecision = t.OccurredPrecision.ToString(),
            Label = t.Label,
            Description = t.Description,
            Marker = t.Marker.ToString(),
            WhatWasKnownThen = t.WhatWasKnownThen,
            TextAlternative = t.TextAlternative,
        }).ToList());
    }

    /// <summary>
    /// People &amp; Power (design 1i), tiered by leverage over a named decision.
    ///
    /// <paramref name="decision"/> selects an alternative tiering; omitting it gives the
    /// room's default. Actors with no role for the requested decision fall back to their
    /// default row rather than vanishing — a re-sort that silently drops actors would
    /// misrepresent who is involved.
    /// </summary>
    [HttpGet("{slug}/actors")]
    public async Task<ActionResult<RoomActorsDto>> Actors(
        string slug, [FromQuery] string? decision, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var roles = await _db.ActorRoomRoles.AsNoTracking()
            .Include(r => r.Actor)
            .Where(r => r.RoomId == room.Id)
            .ToListAsync(ct);

        var chosen = roles
            .Where(r => r.DecisionKey == decision)
            .ToDictionary(r => r.ActorId);

        foreach (var fallback in roles.Where(r => r.DecisionKey == null))
        {
            chosen.TryAdd(fallback.ActorId, fallback);
        }

        var actorIds = chosen.Keys.ToList();
        var appearances = await _db.ObjectLinks.AsNoTracking()
            .Where(l => l.ToType == ObjectType.Actor
                     && actorIds.Contains(l.ToId)
                     && l.ValidTo == null)
            .GroupBy(l => l.ToId)
            .Select(g => new { ActorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActorId, x => x.Count, ct);

        RoomActorDto Map(ActorRoomRole r) => new()
        {
            Id = r.ActorId,
            Slug = r.Actor?.Slug ?? "",
            Name = r.Actor?.Name ?? "",
            ActorType = r.Actor?.ActorType.ToString() ?? "",
            Tier = r.Tier.ToString(),
            RoleHere = r.RoleHere,
            ActualPower = r.Actor?.ActualPower ?? "",
            StatedWants = r.Actor?.StatedWants,
            StatedWantsAsOf = r.Actor?.StatedWantsAsOf,
            StatedWantsSourceRefId = r.Actor?.StatedWantsSourceRefId,
            ConstrainedBy = r.Actor?.ConstrainedBy ?? "",
            LeverageStatement = r.LeverageStatement,
            AppearanceCount = appearances.TryGetValue(r.ActorId, out var n) ? n : 0,
        };

        List<RoomActorDto> Tier(ActorTier tier) => chosen.Values
            .Where(r => r.Tier == tier)
            .OrderBy(r => r.Ordinal)
            .Select(Map)
            .ToList();

        return Ok(new RoomActorsDto
        {
            DecisionKey = decision,
            AvailableDecisions = roles
                .Where(r => r.DecisionKey is not null)
                .Select(r => r.DecisionKey!)
                .Distinct().OrderBy(d => d).ToList(),
            Decides = Tier(ActorTier.Decides),
            Shapes = Tier(ActorTier.Shapes),
            Constrained = Tier(ActorTier.Constrained),
        });
    }

    /// <summary>
    /// Record that this reader has seen the room at a revision.
    ///
    /// Anonymous callers are accepted deliberately: PRD 01 §TR-5 wants "since your last
    /// visit" to work without an account, and Civic already gives anonymous clients a stable
    /// id. Never moves backwards — opening an old share link does not un-see a room.
    /// </summary>
    [HttpPost("{slug}/seen")]
    public async Task<IActionResult> MarkSeen(
        string slug, [FromBody] MarkSeenRequest body, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        await _revisions.MarkSeenAsync(
            _user.GetCurrentUserId(), room.Id, body.Revision ?? room.Revision, ct);

        return NoContent();
    }

    /// <summary>
    /// Follow a room. Account-bound, so it needs a verified email like every other
    /// persistent write in Civic — a follow that survives is a notification commitment.
    /// </summary>
    [HttpPost("{slug}/follow")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> Follow(string slug, CancellationToken ct)
        => SetFollowAsync(slug, true, ct);

    [HttpDelete("{slug}/follow")]
    [Authorize(Policy = "VerifiedEmail")]
    public Task<IActionResult> Unfollow(string slug, CancellationToken ct)
        => SetFollowAsync(slug, false, ct);

    /// <summary>
    /// The ambient path (design 1a). Nothing gates on this — the bars only remember.
    /// </summary>
    [HttpPost("{slug}/sections/{sectionKey}/progress")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> SectionProgress(
        string slug, string sectionKey, [FromBody] SectionProgressRequest body, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var state = await GetOrCreateStateAsync(room.Id, ct);

        var progress = state.SectionProgress.FirstOrDefault(p => p.SectionKey == sectionKey);
        if (progress is null)
        {
            progress = new Models.Rooms.SectionProgress { SectionKey = sectionKey };
            state.SectionProgress.Add(progress);
        }

        progress.Opened = true;
        progress.LastOpenedAt = DateTime.UtcNow;
        // Monotonic: revisiting a section you have already read through must not walk the bar back.
        progress.ItemsSeen = Math.Max(progress.ItemsSeen, body.ItemsSeen);
        if (body.ItemsTotal > 0) progress.ItemsTotal = body.ItemsTotal;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// The density dial. Global, not per room — design 1c is explicit that it is remembered
    /// per user. Board becomes the default only after two consecutive Board choices, and the
    /// dial never auto-switches mid-session.
    /// </summary>
    [HttpPut("density")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<IActionResult> SetDensity(
        [FromBody] SetDensityRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<RoomDensity>(body.Density, ignoreCase: true, out var density))
        {
            return BadRequest(new { error = "Unknown density.", code = "bad_density" });
        }

        var userId = _user.GetCurrentUserId();
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound(new { error = "No profile for this user." });

        profile.RoomDensityConsecutiveBoard = density == RoomDensity.Board
            ? profile.RoomDensityConsecutiveBoard + 1
            : 0;
        profile.RoomDensity = density;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // --- helpers ---------------------------------------------------------------------

    private async Task<IActionResult> SetFollowAsync(string slug, bool following, CancellationToken ct)
    {
        var room = await _rooms.FindBySlugAsync(slug, await ViewerLocalityAsync(ct), ct);
        if (room is null) return NotFound();

        var state = await GetOrCreateStateAsync(room.Id, ct);
        state.Following = following;
        state.FollowedAt = following ? DateTime.UtcNow : null;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<UserRoomState> GetOrCreateStateAsync(Guid roomId, CancellationToken ct)
    {
        var userId = _user.GetCurrentUserId();
        var state = await _db.UserRoomStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.RoomId == roomId, ct);

        if (state is null)
        {
            state = new UserRoomState { Id = Guid.NewGuid(), UserId = userId, RoomId = roomId };
            _db.UserRoomStates.Add(state);
        }

        return state;
    }

    /// <summary>The reader's state code, for the locality read-wall. Null = national only.</summary>
    private Task<string?> ViewerLocalityAsync(CancellationToken ct)
    {
        var userId = _user.GetCurrentUserId();
        return _db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.LocalityState)
            .FirstOrDefaultAsync(ct);
    }
}

public class MarkSeenRequest
{
    /// <summary>Omit to mark the room's current revision as seen.</summary>
    public int? Revision { get; set; }
}

public class SectionProgressRequest
{
    public int ItemsSeen { get; set; }
    public int ItemsTotal { get; set; }
}

public class SetDensityRequest
{
    public string Density { get; set; } = "Read";
}

/// <summary>One mark on the revision scrubber.</summary>
public class RoomRevisionMarkDto
{
    public int Revision { get; set; }
    public bool IsMeaningful { get; set; }
    public string Summary { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool HasSnapshot { get; set; }
}
