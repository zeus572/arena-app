using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Arena.Shared.Llm;
using Civic.API.Models;
using Civic.API.Services;
using Civic.API.Services.Coalition.Product;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Coalition game API — the daily acts (position / amend / co-sign / decline) feed the
/// SAME state machine agents use; the read model surfaces the spectrum bar + state.
/// Constructed agents provide ballast (no LLM). All endpoints are broadcast-only.
/// </summary>
[ApiController]
[Route("api/coalition/provisions")]
public class CoalitionProvisionsController : ControllerBase
{
    private readonly CoalitionLoopService _loop;
    private readonly CoalitionSeeder _seeder;
    private readonly ICurrentUserService _user;
    private readonly IWebHostEnvironment _env;

    public CoalitionProvisionsController(CoalitionLoopService loop, CoalitionSeeder seeder, ICurrentUserService user, IWebHostEnvironment env)
    {
        _loop = loop;
        _seeder = seeder;
        _user = user;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProvisionSummaryDto>>> List(CancellationToken ct)
        => Ok(await _loop.ListAsync(_user.GetCurrentUserId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProvisionDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var detail = await _loop.GetDetailAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/join")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ProvisionDetailDto>> Join(Guid id, [FromBody] JoinRequest req, CancellationToken ct)
    {
        await _loop.JoinAsync(id, _user.GetCurrentUserId(), req.Bucket, req.AgeBand, ct);
        var detail = await _loop.GetDetailAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/positions")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ProvisionDetailDto>> Position(Guid id, [FromBody] PositionRequest req, CancellationToken ct)
    {
        var detail = await _loop.TakePositionAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/amendments")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ProvisionDetailDto>> Amend(Guid id, [FromBody] AmendmentRequest req, CancellationToken ct)
    {
        var detail = await _loop.ProposeAmendmentAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Free-form amendment: write natural-language text; extraction maps it to positions.</summary>
    [HttpPost("{id:guid}/amendments/freeform")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ProvisionDetailDto>> AmendFreeform(Guid id, [FromBody] FreeformAmendmentRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest(new { error = "Text is required." });
        var detail = await _loop.ProposeFreeformAmendmentAsync(id, _user.GetCurrentUserId(), req.Text, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/acceptances")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ProvisionDetailDto>> Accept(Guid id, [FromBody] AcceptanceRequest req, CancellationToken ct)
    {
        var detail = await _loop.CastAcceptanceAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Record a daily/scarce act on a provision (reaction-with-reason, steelman, claim-tag, etc.) and earn points.</summary>
    [HttpPost("{id:guid}/acts")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ActResultDto>> Act(Guid id, [FromBody] ActRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<CoalitionActType>(req.Type, ignoreCase: true, out var type))
            return BadRequest(new { error = "Unknown act type." });
        var (points, currency) = await _loop.RecordActAsync(_user.GetCurrentUserId(), id, type, req.Payload, ct, req.VersionId);
        return Ok(new ActResultDto(points, currency));
    }

    /// <summary>Record a non-provision act (e.g. longform, author-a-provision draft) and earn points.</summary>
    [HttpPost("/api/coalition/acts")]
    [Authorize(Policy = "VerifiedEmail")]
    public async Task<ActionResult<ActResultDto>> GlobalAct([FromBody] ActRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<CoalitionActType>(req.Type, ignoreCase: true, out var type))
            return BadRequest(new { error = "Unknown act type." });
        var (points, currency) = await _loop.RecordActAsync(_user.GetCurrentUserId(), null, type, req.Payload, ct);
        return Ok(new ActResultDto(points, currency));
    }

    /// <summary>Manually run one round of agent (ballast) acts. Development-only — in prod the
    /// lifecycle scheduler runs agent ballast automatically.</summary>
    [HttpPost("{id:guid}/agent-step")]
    public async Task<ActionResult<ProvisionDetailDto>> AgentStep(Guid id, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        var detail = await _loop.AgentStepAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Dev helper: (re)seed the demo provisions. Development-only.</summary>
    [HttpPost("/api/coalition/seed")]
    public async Task<ActionResult> Seed(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        await _seeder.SeedAsync(ct);
        return Ok(new { seeded = true });
    }

    /// <summary>The provision's two framings — cultural vs governance (doc 02).</summary>
    [HttpGet("{id:guid}/framings")]
    public async Task<ActionResult<FramingsDto>> Framings(Guid id, CancellationToken ct)
    {
        var f = await _loop.GetFramingsAsync(id, ct);
        return f is null ? NotFound() : Ok(f);
    }

    /// <summary>Manually birth a provision from a briefing. Development-only — in prod the
    /// lifecycle scheduler births provisions from the briefing catalog automatically.</summary>
    [HttpPost("/api/coalition/birth")]
    public async Task<ActionResult<ProvisionDetailDto>> Birth([FromBody] BirthRequest req, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        try
        {
            var detail = await _loop.BirthFromBriefingAsync(req.BriefingId, _user.GetCurrentUserId(), ct);
            return detail is null ? NotFound(new { error = "Briefing not found." }) : Ok(detail);
        }
        catch (LlmException ex) when (ex.Kind is LlmFailureKind.CallFailed or LlmFailureKind.BadResponse)
        {
            // The LLM was configured but couldn't produce a usable result — either the live call
            // failed (e.g. out of credits) or the model returned unparseable content. Either way,
            // don't birth a dead heuristic provision — surface a retryable error instead.
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Generation service is temporarily unavailable. Try again shortly." });
        }
    }

    // ---- Layer 3 gamification ----

    /// <summary>The current player's record, breadth meter, governance ratio, skill, cadence, league + recommendations.</summary>
    [HttpGet("/api/coalition/me")]
    public async Task<ActionResult<MeDto>> Me(CancellationToken ct)
        => Ok(await _loop.GetMeAsync(_user.GetCurrentUserId(), ct));

    /// <summary>Composed circles (skill/engagement cohorts) with breadth-favoring standings.</summary>
    [HttpGet("/api/coalition/circles")]
    public async Task<ActionResult<IReadOnlyList<CircleDto>>> Circles(CancellationToken ct)
        => Ok(await _loop.GetCirclesAsync(ct));

    /// <summary>The player's daily quests with server-computed completion (from the acts
    /// ledger). Reading this also grants the reward XP for any freshly-completed quest,
    /// once per day.</summary>
    [HttpGet("/api/coalition/quests")]
    public async Task<ActionResult<IReadOnlyList<QuestDto>>> Quests(CancellationToken ct)
        => Ok(await _loop.GetQuestsAsync(_user.GetCurrentUserId(), ct));

    /// <summary>Dev helper: (re)compose circles from the current player pool. Development-only
    /// (the lifecycle scheduler composes/re-balances circles automatically in prod).</summary>
    [HttpPost("/api/coalition/circles/compose")]
    public async Task<ActionResult> ComposeCircles(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        await _loop.ComposeCirclesAsync(4, ct);
        return Ok(await _loop.GetCirclesAsync(ct));
    }
}
