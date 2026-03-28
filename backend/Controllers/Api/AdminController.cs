using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly HeartbeatSettings _heartbeat;
    private readonly IConfiguration _config;

    public AdminController(ArenaDbContext db, HeartbeatSettings heartbeat, IConfiguration config)
    {
        _db = db;
        _heartbeat = heartbeat;
        _config = config;
    }

    [HttpPut("users/{id:guid}/plan")]
    public async Task<IActionResult> SetPlan(Guid id, [FromBody] SetPlanRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!Enum.TryParse<UserPlan>(request.Plan, true, out var plan))
            return BadRequest(new { error = "Invalid plan. Use 'Free' or 'Premium'." });

        user.Plan = plan;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Email, Plan = user.Plan.ToString() });
    }

    [HttpPut("topics/{id:guid}/status")]
    public async Task<IActionResult> SetTopicStatus(Guid id, [FromBody] SetTopicStatusRequest request)
    {
        var topic = await _db.TopicProposals.FindAsync(id);
        if (topic is null) return NotFound();

        if (!Enum.TryParse<TopicStatus>(request.Status, true, out var status))
            return BadRequest(new { error = "Invalid status." });

        topic.Status = status;
        topic.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { topic.Id, topic.Title, Status = topic.Status.ToString() });
    }
    [AllowAnonymous]
    [HttpGet("heartbeat")]
    public IActionResult GetHeartbeat()
    {
        return Ok(new
        {
            enabled = _heartbeat.Enabled,
            intervalSeconds = _heartbeat.IntervalSeconds,
            model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514",
        });
    }

    [HttpPut("heartbeat")]
    public IActionResult UpdateHeartbeat([FromBody] UpdateHeartbeatRequest request)
    {
        if (request.Enabled.HasValue)
            _heartbeat.Enabled = request.Enabled.Value;
        if (request.IntervalSeconds.HasValue)
        {
            if (request.IntervalSeconds.Value < 60)
                return BadRequest(new { error = "Interval must be at least 60 seconds." });
            _heartbeat.IntervalSeconds = request.IntervalSeconds.Value;
        }

        return Ok(new
        {
            enabled = _heartbeat.Enabled,
            intervalSeconds = _heartbeat.IntervalSeconds,
            model = _config["Anthropic:Model"] ?? "claude-sonnet-4-20250514",
        });
    }
}

public record SetPlanRequest(string Plan);
public record SetTopicStatusRequest(string Status);
public record UpdateHeartbeatRequest(bool? Enabled, int? IntervalSeconds);
