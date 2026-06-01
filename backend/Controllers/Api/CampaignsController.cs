using Microsoft.AspNetCore.Mvc;
using Arena.API.Models.DTOs;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class CampaignsController : ControllerBase
{
    private readonly ICurrentUserService _userService;
    private readonly CampaignService _campaigns;

    public CampaignsController(ICurrentUserService userService, CampaignService campaigns)
    {
        _userService = userService;
        _campaigns = campaigns;
    }

    /// <summary>Static list of preset candidate personas. No auth required.</summary>
    [HttpGet("personas")]
    public IActionResult GetPersonas()
    {
        var personas = PersonaBank.All.Select(p => new PersonaDto
        {
            Key = p.Key,
            Name = p.Name,
            Persona = p.Persona,
            Theme = p.Theme,
            OpponentName = p.OpponentName,
            OpponentPersona = p.OpponentPersona,
        });
        return Ok(personas);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            var detail = await _campaigns.CreateAsync(user, request);
            return CreatedAtAction(nameof(GetById), new { id = detail.Campaign.Id }, detail);
        }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var user = await _userService.GetOrCreateUserAsync();
        var list = await _campaigns.ListAsync(user.Id);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.GetDetailAsync(id, user.Id));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/advance")]
    public async Task<IActionResult> Advance(Guid id, [FromBody] AdvanceWeekRequest request)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.AdvanceWeekAsync(id, user.Id, request.Activities));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/allocate")]
    public async Task<IActionResult> Allocate(Guid id, [FromBody] AdvanceWeekRequest request)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.PreviewAllocationAsync(id, user.Id, request.Activities));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/events/{eventId:guid}/respond")]
    public async Task<IActionResult> RespondEvent(Guid id, Guid eventId, [FromBody] RespondEventRequest request)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.ResolveEventAsync(id, user.Id, eventId, request.OptionId));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/debate")]
    public async Task<IActionResult> RunDebate(Guid id, [FromBody] RunDebateRequest request)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.RunDebateMilestoneAsync(id, user.Id, request.Skip, request.Topic));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CampaignConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> GetResults(Guid id)
    {
        var user = await _userService.GetOrCreateUserAsync();
        try
        {
            return Ok(await _campaigns.GetResultsAsync(id, user.Id));
        }
        catch (CampaignNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (CampaignValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
