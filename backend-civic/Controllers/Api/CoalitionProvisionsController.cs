using Microsoft.AspNetCore.Mvc;
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

    public CoalitionProvisionsController(CoalitionLoopService loop, CoalitionSeeder seeder, ICurrentUserService user)
    {
        _loop = loop;
        _seeder = seeder;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProvisionSummaryDto>>> List(CancellationToken ct)
        => Ok(await _loop.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProvisionDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var detail = await _loop.GetDetailAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<ProvisionDetailDto>> Join(Guid id, [FromBody] JoinRequest req, CancellationToken ct)
    {
        await _loop.JoinAsync(id, _user.GetCurrentUserId(), req.Bucket, ct);
        var detail = await _loop.GetDetailAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/positions")]
    public async Task<ActionResult<ProvisionDetailDto>> Position(Guid id, [FromBody] PositionRequest req, CancellationToken ct)
    {
        var detail = await _loop.TakePositionAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/amendments")]
    public async Task<ActionResult<ProvisionDetailDto>> Amend(Guid id, [FromBody] AmendmentRequest req, CancellationToken ct)
    {
        var detail = await _loop.ProposeAmendmentAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id:guid}/acceptances")]
    public async Task<ActionResult<ProvisionDetailDto>> Accept(Guid id, [FromBody] AcceptanceRequest req, CancellationToken ct)
    {
        var detail = await _loop.CastAcceptanceAsync(id, _user.GetCurrentUserId(), req, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Run one round of agent (ballast) acts — agents take positions, decline, propose carve-outs, co-sign.</summary>
    [HttpPost("{id:guid}/agent-step")]
    public async Task<ActionResult<ProvisionDetailDto>> AgentStep(Guid id, CancellationToken ct)
    {
        var detail = await _loop.AgentStepAsync(id, _user.GetCurrentUserId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Dev helper: (re)seed the demo provisions.</summary>
    [HttpPost("/api/coalition/seed")]
    public async Task<ActionResult> Seed(CancellationToken ct)
    {
        await _seeder.SeedAsync(ct);
        return Ok(new { seeded = true });
    }
}
