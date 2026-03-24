using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public AdminController(ArenaDbContext db) => _db = db;

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
}

public record SetPlanRequest(string Plan);
public record SetTopicStatusRequest(string Status);
