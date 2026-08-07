using System.Text.Json.Nodes;
using Civic.API.Data;
using Civic.API.Models;
using Civic.API.Models.Rooms;
using Civic.API.Services;
using Civic.API.Services.Coalition.Product;
using Civic.API.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Room interactions (PRD 06).
///
/// Anonymous readers can play everything. They store nothing and earn nothing, which is the
/// same bargain the daily games strike — the learning is the point, and gating it behind an
/// account would put a signup wall in front of the one part of the product that teaches.
/// </summary>
[ApiController]
[Route("api/rooms/{roomSlug}/interactions")]
public class RoomInteractionsController : ControllerBase
{
    private const int MinShareCount = 20;

    private readonly CivicDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ReasoningLedger _ledger;

    public RoomInteractionsController(
        CivicDbContext db, ICurrentUserService user, ReasoningLedger ledger)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
    }

    /// <summary>Playable interactions for a room, with answer keys stripped.</summary>
    [HttpGet]
    public async Task<ActionResult<List<InteractionDto>>> List(string roomSlug, CancellationToken ct)
    {
        var room = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Slug == roomSlug, ct);
        if (room is null) return NotFound();

        var userId = _user.GetCurrentUserId();

        var interactions = await _db.Interactions.AsNoTracking()
            .Where(i => i.RoomId == room.Id
                     && (i.Status == RoomStatus.Published || i.Status == RoomStatus.Monitoring))
            .OrderBy(i => i.Ordinal)
            .ToListAsync(ct);

        var ids = interactions.Select(i => i.Id).ToList();
        var plays = await _db.RoomInteractionPlays.AsNoTracking()
            .Where(p => ids.Contains(p.InteractionId) && p.UserId == userId)
            .ToListAsync(ct);

        return Ok(interactions.Select(i => ToDto(i, plays)).ToList());
    }

    /// <summary>
    /// Submit a response.
    ///
    /// The Pre phase of Vote Before Reading does NOT echo the answer back. Hiding the first
    /// vote until both sides have been read is the entire mechanic — a client that showed
    /// it would defeat the interaction, so the server refuses to send it rather than
    /// trusting the client not to render it.
    /// </summary>
    [HttpPost("{slug}/submit")]
    public async Task<ActionResult<SubmitResultDto>> Submit(
        string roomSlug, string slug, [FromBody] SubmitRequest body, CancellationToken ct)
    {
        var interaction = await _db.Interactions
            .FirstOrDefaultAsync(i => i.Slug == slug, ct);
        if (interaction is null) return NotFound();

        if (interaction.Status is RoomStatus.Draft or RoomStatus.Candidate)
        {
            return NotFound();
        }

        var phase = body.Phase?.Equals("pre", StringComparison.OrdinalIgnoreCase) == true
            ? InteractionPhase.Pre
            : InteractionPhase.Post;

        var result = Score(interaction, body.ResponseJson);
        if (result is null)
        {
            return BadRequest(new { error = "Response did not match this interaction.", code = "bad_response" });
        }

        var userId = _user.GetCurrentUserId();
        var anonymous = string.Equals(userId, "anonymous", StringComparison.OrdinalIgnoreCase);

        var dto = new SubmitResultDto
        {
            Kind = interaction.Kind.ToString(),
            Phase = phase.ToString(),
            Scored = result.Scored,
            Score = result.Scored ? result.Score : null,
            Explanation = string.IsNullOrWhiteSpace(result.Explanation)
                ? interaction.Explanation
                : result.Explanation,
            Items = result.Items.Select(i => new ItemResultDto
            {
                ItemId = i.ItemId,
                Correct = i.Correct,
                Explanation = i.Explanation,
                CorrectLabel = i.CorrectLabel,
            }).ToList(),
        };

        // Vote Before Reading, first pass: acknowledge and withhold.
        if (interaction.Kind == InteractionKind.VoteBeforeReading && phase == InteractionPhase.Pre)
        {
            dto.Items = new List<ItemResultDto>();
            dto.Explanation =
                "Recorded. We will show you this answer again after you have read both sides.";
        }

        if (anonymous)
        {
            // Played fully, stored not at all.
            dto.Persisted = false;
            return Ok(dto);
        }

        var existing = await _db.RoomInteractionPlays.FirstOrDefaultAsync(
            p => p.InteractionId == interaction.Id && p.UserId == userId && p.Phase == phase, ct);

        var firstCompletion = false;

        if (existing is null)
        {
            existing = new RoomInteractionPlay
            {
                Id = Guid.NewGuid(),
                InteractionId = interaction.Id,
                UserId = userId,
                Phase = phase,
            };
            _db.RoomInteractionPlays.Add(existing);
        }

        existing.ResponseJson = body.ResponseJson ?? "{}";
        existing.Score = result.Score;

        if (phase == InteractionPhase.Post && !existing.Completed)
        {
            existing.Completed = true;
            firstCompletion = true;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two tabs racing the same submit; the unique index settled it.
            _db.ChangeTracker.Clear();
            firstCompletion = false;
        }

        // XP once, on the transition to completed — the Post row is the idempotency guard.
        if (firstCompletion)
        {
            await _ledger.RecordAsync(
                userId, CoalitionActType.RoomInteraction,
                payload: $"interaction:{interaction.Id}", ct: ct);
        }

        // Vote Before Reading, second pass: now show both answers and whether they moved.
        if (interaction.Kind == InteractionKind.VoteBeforeReading && phase == InteractionPhase.Post)
        {
            var pre = await _db.RoomInteractionPlays.AsNoTracking().FirstOrDefaultAsync(
                p => p.InteractionId == interaction.Id
                  && p.UserId == userId
                  && p.Phase == InteractionPhase.Pre, ct);

            if (pre is not null)
            {
                dto.PriorResponseJson = pre.ResponseJson;
                dto.Moved = !string.Equals(pre.ResponseJson, existing.ResponseJson, StringComparison.Ordinal);
            }
        }

        dto.Persisted = true;
        return Ok(dto);
    }

    private static InteractionResult? Score(Interaction interaction, string? responseJson)
    {
        var json = responseJson ?? "{}";

        switch (interaction.Kind)
        {
            case InteractionKind.BeforeYouKnow:
            {
                var payload = InteractionJson.Parse<BeforeYouKnowPayload>(interaction.PayloadJson);
                var response = InteractionJson.Parse<BeforeYouKnowResponse>(json);
                if (payload is null || response is null) return null;
                return InteractionScoring.ScoreBeforeYouKnow(payload, response);
            }
            case InteractionKind.ClassifyStatement:
            {
                var payload = InteractionJson.Parse<ClassifyStatementPayload>(interaction.PayloadJson);
                var response = InteractionJson.Parse<ClassifyStatementResponse>(json);
                if (payload is null || response is null) return null;
                return InteractionScoring.ScoreClassifyStatement(payload, response);
            }
            case InteractionKind.TimelineBuilder:
            {
                var payload = InteractionJson.Parse<TimelineBuilderPayload>(interaction.PayloadJson);
                var response = InteractionJson.Parse<TimelineBuilderResponse>(json);
                if (payload is null || response is null) return null;
                return InteractionScoring.ScoreTimelineBuilder(payload, response);
            }
            case InteractionKind.VoteBeforeReading:
            {
                var response = InteractionJson.Parse<VoteBeforeReadingResponse>(json);
                if (response is null) return null;
                return InteractionScoring.ScoreVoteBeforeReading(response);
            }
            default:
                return null;
        }
    }

    private static InteractionDto ToDto(Interaction i, List<RoomInteractionPlay> plays) => new()
    {
        Slug = i.Slug,
        Kind = i.Kind.ToString(),
        Title = i.Title,
        Prompt = i.Prompt,
        LearningObjective = i.LearningObjective,
        Scored = InteractionScoring.IsScored(i.Kind),
        PredictionId = i.PredictionId,
        // Rebuilt from allow-listed fields, never copied-and-scrubbed.
        Payload = InteractionRedaction.ForPlayer(i.Kind, i.PayloadJson),
        PlayedPre = plays.Any(p => p.InteractionId == i.Id && p.Phase == InteractionPhase.Pre),
        PlayedPost = plays.Any(p => p.InteractionId == i.Id && p.Phase == InteractionPhase.Post),
    };
}

public class SubmitRequest
{
    /// <summary>"pre" or "post". Defaults to post.</summary>
    public string? Phase { get; set; }
    public string? ResponseJson { get; set; }
}

public class InteractionDto
{
    public string Slug { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string LearningObjective { get; set; } = "";
    public bool Scored { get; set; }
    public Guid? PredictionId { get; set; }
    /// <summary>Answer key stripped.</summary>
    public JsonNode? Payload { get; set; }
    public bool PlayedPre { get; set; }
    public bool PlayedPost { get; set; }
}

public class ItemResultDto
{
    public string ItemId { get; set; } = "";
    public bool Correct { get; set; }
    /// <summary>Present whether the answer was right or wrong. PRD 06 makes this mandatory.</summary>
    public string Explanation { get; set; } = "";
    public string? CorrectLabel { get; set; }
}

public class SubmitResultDto
{
    public string Kind { get; set; } = "";
    public string Phase { get; set; } = "";
    public bool Scored { get; set; }
    /// <summary>Null for the unscored kinds, rather than a misleading zero.</summary>
    public int? Score { get; set; }
    public string Explanation { get; set; } = "";
    public List<ItemResultDto> Items { get; set; } = new();
    /// <summary>False for anonymous play — nothing was stored and nothing was earned.</summary>
    public bool Persisted { get; set; }
    /// <summary>Vote Before Reading, post phase only.</summary>
    public string? PriorResponseJson { get; set; }
    public bool? Moved { get; set; }
}
