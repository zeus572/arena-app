using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Services.Rooms;

/// <summary>
/// Loads hand-authored pilot rooms from embedded Seed/rooms/*.json.
///
/// Two passes: create-or-find every referenced object by slug, then wire the graph edges.
/// Idempotent — running it twice changes nothing, which matters because it runs on every
/// startup behind <c>Rooms:SeedPilot</c>.
///
/// Everything it writes is stamped ProvenanceOrigin.Seed, and the room lands at whatever
/// <c>Rooms:PilotStatus</c> says (Draft by default). The seeded room is a structural
/// fixture and a test corpus. It is deliberately NOT published content, and
/// <c>Rooms:SeedPilot</c> defaults to false so production never creates it by accident.
/// </summary>
public class RoomSeeder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CivicDbContext _db;
    private readonly ObjectLinkService _links;
    private readonly IConfiguration _config;
    private readonly ILogger<RoomSeeder> _log;

    public RoomSeeder(
        CivicDbContext db,
        ObjectLinkService links,
        IConfiguration config,
        ILogger<RoomSeeder> log)
    {
        _db = db;
        _links = links;
        _config = config;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!_config.GetValue("Rooms:SeedPilot", false))
        {
            _log.LogInformation("Rooms:SeedPilot is off; skipping pilot room seed.");
            return;
        }

        var status = _config.GetValue("Rooms:PilotStatus", "Draft");
        if (!Enum.TryParse<RoomStatus>(status, ignoreCase: true, out var roomStatus))
        {
            roomStatus = RoomStatus.Draft;
        }

        foreach (var resource in EmbeddedRoomFiles())
        {
            var file = LoadJson<RoomSeedFile>(resource);
            if (file is null) continue;

            await SeedFileAsync(file, roomStatus, ct);
        }
    }

    /// <summary>Exposed so tests can drive one file deterministically.</summary>
    public async Task SeedFileAsync(
        RoomSeedFile file, RoomStatus status, CancellationToken ct = default)
    {
        var sources = await UpsertSourcesAsync(file.Sources, ct);
        var concepts = await UpsertConceptsAsync(file.Concepts, ct);
        var actors = await UpsertActorsAsync(file.Actors, sources, ct);
        var claims = await UpsertClaimsAsync(file.Claims, ct);

        // Claim edges need every claim, source and actor to exist first — hence two passes.
        await LinkClaimsAsync(file.Claims, claims, sources, actors, ct);

        var storyIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var story in file.Stories)
        {
            storyIds[story.Slug] = await UpsertStoryAsync(story, status, claims, concepts, ct);
        }

        if (file.Theme is not null)
        {
            await UpsertThemeAsync(
                file.Theme, status, claims, concepts, actors, file.Actors, storyIds, ct);
        }

        _log.LogInformation(
            "Seeded pilot room content: {Sources} sources, {Concepts} concepts, {Actors} actors, "
          + "{Claims} claims, {Stories} stories.",
            sources.Count, concepts.Count, actors.Count, claims.Count, storyIds.Count);
    }

    // ------------------------------------------------------------------ objects

    private async Task<Dictionary<string, Guid>> UpsertSourcesAsync(
        List<SeedSource> seeds, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in seeds)
        {
            var hash = Sha256(s.Url.Trim().ToLowerInvariant());
            var existing = await _db.SourceRefs.FirstOrDefaultAsync(x => x.UrlHash == hash, ct);

            if (existing is null)
            {
                existing = new SourceRef
                {
                    Id = Guid.NewGuid(),
                    Url = s.Url,
                    UrlHash = hash,
                    Title = s.Title,
                    Organization = s.Organization,
                    SourceType = ParseEnum(s.SourceType, SourceType.Reporting),
                    IsPrimary = s.IsPrimary,
                    PublishedAt = s.PublishedAt,
                    FullTextAvailable = s.FullTextAvailable,
                };
                _db.SourceRefs.Add(existing);
            }

            map[s.Key] = existing.Id;
        }

        await _db.SaveChangesAsync(ct);
        return map;
    }

    private async Task<Dictionary<string, Guid>> UpsertConceptsAsync(
        List<SeedConcept> seeds, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in seeds)
        {
            var existing = await _db.Concepts.FirstOrDefaultAsync(x => x.Slug == c.Slug, ct);

            if (existing is null)
            {
                existing = new Concept { Id = Guid.NewGuid(), Slug = c.Slug };
                _db.Concepts.Add(existing);
            }

            existing.Title = c.Title;
            existing.Category = c.Category;
            existing.KnowledgeKind = ParseEnum(c.KnowledgeKind, KnowledgeKind.Concept);
            existing.ShortGloss = c.ShortGloss;
            existing.PlainDefinition = c.PlainDefinition;
            existing.WhyItMatters = c.WhyItMatters;
            existing.CurrentExample = c.CurrentExample;
            existing.CommonMisunderstanding = c.CommonMisunderstanding;
            existing.TryItQuestion = c.TryItQuestion;
            existing.ConfusionPairSlug = c.ConfusionPairSlug;
            existing.ConfusionDiscriminator = c.ConfusionDiscriminator;
            existing.GenerationSource = CivicGenerationSource.Seed;

            map[c.Slug] = existing.Id;
        }

        await _db.SaveChangesAsync(ct);
        return map;
    }

    private async Task<Dictionary<string, Guid>> UpsertActorsAsync(
        List<SeedActor> seeds, Dictionary<string, Guid> sources, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in seeds)
        {
            var existing = await _db.Actors.FirstOrDefaultAsync(x => x.Slug == a.Slug, ct);

            if (existing is null)
            {
                existing = new Actor { Id = Guid.NewGuid(), Slug = a.Slug };
                _db.Actors.Add(existing);
            }

            existing.Name = a.Name;
            existing.ActorType = ParseEnum(a.ActorType, ActorType.GovernmentBody);
            existing.AlternateNames = a.AlternateNames;
            existing.ActualPower = a.ActualPower;
            existing.ConstrainedBy = a.ConstrainedBy;
            existing.StatedWants = a.StatedWants;
            existing.StatedWantsAsOf = a.StatedWantsAsOf;
            existing.StatedWantsSourceRefId =
                a.StatedWantsSourceKey is { } k && sources.TryGetValue(k, out var sid) ? sid : null;
            existing.GenerationSource = CivicGenerationSource.Seed;
            existing.Provenance = new List<FieldProvenance>
            {
                new() { Field = nameof(Actor.StatedWants), ProposedBy = ProvenanceOrigin.Seed },
            };

            map[a.Slug] = existing.Id;
        }

        await _db.SaveChangesAsync(ct);
        return map;
    }

    private async Task<Dictionary<string, Guid>> UpsertClaimsAsync(
        List<SeedClaim> seeds, CancellationToken ct)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in seeds)
        {
            var existing = await _db.Claims.FirstOrDefaultAsync(x => x.Slug == c.Slug, ct);
            var isNew = existing is null;

            if (existing is null)
            {
                existing = new Claim
                {
                    Id = Guid.NewGuid(),
                    Slug = c.Slug,
                    NormalizedTextHash = Sha256(Normalize(c.Text)),
                };
                _db.Claims.Add(existing);
            }

            existing.Text = c.Text;
            existing.Status = ParseEnum(c.Status, ClaimStatus.PlausibleButUnresolved);
            existing.Kind = ParseEnum(c.Kind, ClaimKind.Factual);
            existing.EvidenceSummary = c.EvidenceSummary;
            existing.WhatWouldSettleIt = c.WhatWouldSettleIt;
            existing.GenerationSource = CivicGenerationSource.Seed;
            existing.Provenance = new List<FieldProvenance>
            {
                new() { Field = nameof(Claim.Text), ProposedBy = ProvenanceOrigin.Seed },
            };

            map[c.Slug] = existing.Id;

            if (isNew)
            {
                // Every claim starts its history, so "history of this label" is never empty.
                _db.ClaimStatusHistories.Add(new ClaimStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ClaimId = existing.Id,
                    FromStatus = null,
                    ToStatus = existing.Status,
                    ChangeKind = StatusChangeKind.InitialReview,
                    Rationale = "Initial status from the hand-authored pilot seed.",
                    ChangedBy = "seed",
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return map;
    }

    private async Task LinkClaimsAsync(
        List<SeedClaim> seeds,
        Dictionary<string, Guid> claims,
        Dictionary<string, Guid> sources,
        Dictionary<string, Guid> actors,
        CancellationToken ct)
    {
        foreach (var c in seeds)
        {
            var claimRef = new ObjectRef(ObjectType.Claim, claims[c.Slug]);

            foreach (var key in c.SupportedBy)
            {
                if (!sources.TryGetValue(key, out var id)) continue;
                await _links.LinkAsync(claimRef, LinkRelation.SupportedBy,
                    new ObjectRef(ObjectType.SourceRef, id),
                    proposedBy: ProvenanceOrigin.Seed, ct: ct);
            }

            foreach (var key in c.ContradictedBy)
            {
                if (!sources.TryGetValue(key, out var id)) continue;
                await _links.LinkAsync(claimRef, LinkRelation.ContradictedBy,
                    new ObjectRef(ObjectType.SourceRef, id),
                    proposedBy: ProvenanceOrigin.Seed, ct: ct);
            }

            foreach (var slug in c.AssertedBy)
            {
                if (!actors.TryGetValue(slug, out var id)) continue;
                await _links.LinkAsync(claimRef, LinkRelation.AssertedBy,
                    new ObjectRef(ObjectType.Actor, id),
                    proposedBy: ProvenanceOrigin.Seed, ct: ct);
            }
        }
    }

    // ------------------------------------------------------------------ rooms

    private async Task<Guid> UpsertStoryAsync(
        SeedStoryRoom seed,
        RoomStatus status,
        Dictionary<string, Guid> claims,
        Dictionary<string, Guid> concepts,
        CancellationToken ct)
    {
        var room = await _db.StoryRooms.FirstOrDefaultAsync(r => r.Slug == seed.Slug, ct);

        if (room is null)
        {
            room = new StoryRoom { Id = Guid.NewGuid(), Slug = seed.Slug, Revision = 1 };
            _db.Rooms.Add(room);
        }

        ApplyBase(room, seed, status);
        room.StoryType = ParseEnum(seed.StoryType, RoomTopicCategory.Legislative);
        room.EventTime = seed.EventTime;
        room.EstimatedMinutes = seed.EstimatedMinutes;
        room.HowItWorksIntro = seed.HowItWorksIntro;

        room.WhyItMatters = seed.WhyItMatters.Select(d => new StoryDimension
        {
            Dimension = d.Dimension,
            Text = d.Text,
            ClaimId = d.ClaimSlug is { } s && claims.TryGetValue(s, out var cid) ? cid : null,
        }).ToList();

        room.Stakeholders = seed.Stakeholders.Select(s => new StakeholderImpact
        {
            Group = s.Group,
            ImpactSummary = s.ImpactSummary,
            Confidence = s.Confidence,
        }).ToList();

        room.NextSteps = seed.NextSteps.Select(n => new NextStep
        {
            Description = n.Description,
            VerificationCondition = n.VerificationCondition,
            ExpectedTiming = n.ExpectedTiming,
        }).ToList();

        await _db.SaveChangesAsync(ct);

        var roomRef = new ObjectRef(ObjectType.Room, room.Id);
        await LinkRoomContentAsync(roomRef, seed, claims, concepts, ct);

        var ordinal = 0;
        foreach (var slug in seed.EssentialFactClaims)
        {
            if (!claims.TryGetValue(slug, out var cid)) continue;
            await _links.LinkAsync(roomRef, LinkRelation.EssentialFact,
                new ObjectRef(ObjectType.Claim, cid),
                ordinal: ordinal++, proposedBy: ProvenanceOrigin.Seed, ct: ct);
        }

        return room.Id;
    }

    private async Task UpsertThemeAsync(
        SeedThemeRoom seed,
        RoomStatus status,
        Dictionary<string, Guid> claims,
        Dictionary<string, Guid> concepts,
        Dictionary<string, Guid> actors,
        List<SeedActor> actorSeeds,
        Dictionary<string, Guid> storyIds,
        CancellationToken ct)
    {
        var room = await _db.ThemeRooms.FirstOrDefaultAsync(r => r.Slug == seed.Slug, ct);

        if (room is null)
        {
            room = new ThemeRoom { Id = Guid.NewGuid(), Slug = seed.Slug, Revision = 1 };
            _db.Rooms.Add(room);
        }

        ApplyBase(room, seed, status);
        room.AlternateTitles = seed.AlternateTitles;
        room.MatchTerms = seed.MatchTerms;
        room.ScopeStatement = seed.ScopeStatement;
        room.InclusionRules = seed.InclusionRules;
        room.ExclusionRules = seed.ExclusionRules;
        room.CurrentStatusSentence = seed.CurrentStatusSentence;
        room.TopUnresolvedQuestion = seed.TopUnresolvedQuestion;
        room.WatchNext = seed.WatchNext;
        room.MonitoringCadence = ParseEnum(seed.MonitoringCadence, MonitoringCadence.Weekly);
        room.FreshnessOwner = seed.FreshnessOwner;
        room.ArticlesConsideredCount = seed.ArticlesConsideredCount;
        room.DevelopmentWindowDays = seed.DevelopmentWindowDays;

        room.EssentialFacts = seed.EssentialFacts.Select((f, i) => new EssentialFact
        {
            Text = f.Text,
            ClaimId = f.ClaimSlug is { } s && claims.TryGetValue(s, out var cid) ? cid : null,
            Ordinal = i,
        }).ToList();

        room.TerminologyNotes = seed.TerminologyNotes
            .Select(n => new TerminologyNote { Term = n.Term, Note = n.Note })
            .ToList();

        await _db.SaveChangesAsync(ct);

        // Children are replaced wholesale rather than diffed: the seed file is the source of
        // truth for a seeded room, and a partial merge would leave orphans from an earlier run.
        await ReplaceTimelineAsync(room.Id, seed.Timeline, ct);
        await ReplaceDevelopmentsAsync(room.Id, seed.Developments, storyIds, ct);
        await ReplaceActorRolesAsync(room.Id, seed, actorSeeds, actors, ct);

        var roomRef = new ObjectRef(ObjectType.Room, room.Id);
        await LinkRoomContentAsync(roomRef, seed, claims, concepts, ct);

        var ordinal = 0;
        foreach (var fact in seed.EssentialFacts)
        {
            if (fact.ClaimSlug is not { } slug || !claims.TryGetValue(slug, out var cid)) continue;
            await _links.LinkAsync(roomRef, LinkRelation.EssentialFact,
                new ObjectRef(ObjectType.Claim, cid),
                ordinal: ordinal++, proposedBy: ProvenanceOrigin.Seed, ct: ct);
        }

        foreach (var (_, storyId) in storyIds)
        {
            await _links.LinkAsync(roomRef, LinkRelation.Contains,
                new ObjectRef(ObjectType.Room, storyId),
                proposedBy: ProvenanceOrigin.Seed, ct: ct);
        }
    }

    private async Task LinkRoomContentAsync(
        ObjectRef roomRef,
        SeedRoomBase seed,
        Dictionary<string, Guid> claims,
        Dictionary<string, Guid> concepts,
        CancellationToken ct)
    {
        foreach (var slug in seed.Concepts)
        {
            if (!concepts.TryGetValue(slug, out var id)) continue;
            await _links.LinkAsync(roomRef, LinkRelation.References,
                new ObjectRef(ObjectType.Concept, id),
                proposedBy: ProvenanceOrigin.Seed, ct: ct);
        }

        foreach (var slug in seed.Claims)
        {
            if (!claims.TryGetValue(slug, out var id)) continue;
            await _links.LinkAsync(roomRef, LinkRelation.References,
                new ObjectRef(ObjectType.Claim, id),
                proposedBy: ProvenanceOrigin.Seed, ct: ct);
        }
    }

    private async Task ReplaceTimelineAsync(
        Guid roomId, List<SeedTimelineEvent> seeds, CancellationToken ct)
    {
        var existing = await _db.TimelineEvents.Where(t => t.RoomId == roomId).ToListAsync(ct);
        _db.TimelineEvents.RemoveRange(existing);

        var i = 0;
        foreach (var t in seeds)
        {
            _db.TimelineEvents.Add(new TimelineEvent
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                OccurredOn = t.OccurredOn,
                OccurredPrecision = ParseEnum(t.OccurredPrecision, DatePrecision.Day),
                Label = t.Label,
                Description = t.Description,
                Marker = ParseEnum(t.Marker, TimelineMarker.Agreed),
                WhatWasKnownThen = t.WhatWasKnownThen,
                TextAlternative = t.TextAlternative,
                Ordinal = i++,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceDevelopmentsAsync(
        Guid roomId,
        List<SeedDevelopment> seeds,
        Dictionary<string, Guid> storyIds,
        CancellationToken ct)
    {
        var existing = await _db.Developments.Where(d => d.RoomId == roomId).ToListAsync(ct);
        _db.Developments.RemoveRange(existing);

        var i = 0;
        foreach (var d in seeds)
        {
            _db.Developments.Add(new Development
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                OccurredAt = d.OccurredAt,
                Category = ParseEnum(d.Category, RoomTopicCategory.Legislative),
                Headline = d.Headline,
                Summary = d.Summary,
                WhyItMatters = d.WhyItMatters,
                InclusionReason = d.InclusionReason,
                EvidenceStatus = ParseEnum(d.EvidenceStatus, ClaimStatus.Confirmed),
                StoryRoomId = d.StorySlug is { } s && storyIds.TryGetValue(s, out var sid) ? sid : null,
                Ordinal = i++,
                GenerationSource = CivicGenerationSource.Seed,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task ReplaceActorRolesAsync(
        Guid roomId,
        SeedThemeRoom seed,
        List<SeedActor> actorSeeds,
        Dictionary<string, Guid> actors,
        CancellationToken ct)
    {
        var existing = await _db.ActorRoomRoles.Where(r => r.RoomId == roomId).ToListAsync(ct);
        _db.ActorRoomRoles.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);

        // The room names which actors it tiers; the tiering itself lives on the actor entry.
        var inThisRoom = seed.Actors.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var a in actorSeeds.Where(a => inThisRoom.Contains(a.Slug)))
        {
            if (!actors.TryGetValue(a.Slug, out var actorId)) continue;

            // The default (null-keyed) tiering always exists. Decision keys add rows on top
            // rather than replacing it, so filtering to a decision re-tiers the map instead
            // of an actor being visible only to a reader who already picked the right filter.
            _db.ActorRoomRoles.Add(new ActorRoomRole
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                RoomId = roomId,
                DecisionKey = null,
                Tier = ParseEnum(a.Tier, ActorTier.Shapes),
                LeverageStatement = a.LeverageStatement,
                RoleHere = a.RoleHere,
                Ordinal = a.Ordinal,
            });

            foreach (var d in a.Decisions)
            {
                _db.ActorRoomRoles.Add(new ActorRoomRole
                {
                    Id = Guid.NewGuid(),
                    ActorId = actorId,
                    RoomId = roomId,
                    DecisionKey = d.Key,
                    Tier = ParseEnum(d.Tier ?? a.Tier, ActorTier.Shapes),
                    LeverageStatement = d.LeverageStatement ?? a.LeverageStatement,
                    RoleHere = d.RoleHere ?? a.RoleHere,
                    Ordinal = a.Ordinal,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyBase(Room room, SeedRoomBase seed, RoomStatus status)
    {
        room.Title = seed.Title;
        room.Dek = seed.Dek;
        room.ContentNote = seed.ContentNote;
        room.Locality = seed.Locality;
        room.Sensitivity = ParseEnum(seed.Sensitivity, SensitivityLevel.Standard);
        room.Status = status;
        room.GenerationSource = CivicGenerationSource.Seed;
        room.UpdatedAt = DateTime.UtcNow;
        room.Provenance = new List<FieldProvenance>
        {
            new() { Field = nameof(Room.Title), ProposedBy = ProvenanceOrigin.Seed },
            new() { Field = nameof(Room.Dek), ProposedBy = ProvenanceOrigin.Seed },
        };
    }

    // ------------------------------------------------------------------ plumbing

    private static IEnumerable<string> EmbeddedRoomFiles()
        => Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Where(n => n.Contains(".Seed.rooms.", StringComparison.Ordinal)
                     && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n);

    private T? LoadJson<T>(string resourceName) where T : class
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _log.LogWarning("Embedded room seed {Resource} not found.", resourceName);
            return null;
        }

        return JsonSerializer.Deserialize<T>(stream, JsonOpts);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    /// <summary>Lowercase, collapse whitespace, drop terminal punctuation — so two
    /// wordings of the same assertion hash the same and converge on one claim row.</summary>
    internal static string Normalize(string text)
        => string.Join(' ', text.ToLowerInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', '!', '?');

    internal static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
