using Microsoft.AspNetCore.Mvc;
using Arena.Shared.Social;

namespace Civic.API.Controllers.Api;

/// <summary>
/// Minimal social review + health API (SocialPublisher_Spec §6, §4.4) for the civic publisher.
/// Mirrors the debate app's endpoints; no full review UI this build.
/// </summary>
[ApiController]
public class SocialController : ControllerBase
{
    private readonly SocialReviewService _review;
    private readonly SocialHealthService _health;

    public SocialController(SocialReviewService review, SocialHealthService health)
    {
        _review = review;
        _health = health;
    }

    [HttpGet("api/social/review")]
    public async Task<IActionResult> ListReview(CancellationToken ct)
        => Ok(await _review.ListAwaitingReviewAsync(ct));

    [HttpPost("api/social/review/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var reviewer = User?.Identity?.Name ?? "admin";
        return await _review.ApproveAsync(id, reviewer, ct)
            ? Ok(new { id, status = "Approved" })
            : NotFound(new { id, error = "not awaiting review" });
    }

    [HttpPost("api/social/review/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var reviewer = User?.Identity?.Name ?? "admin";
        return await _review.RejectAsync(id, reviewer, ct)
            ? Ok(new { id, status = "Skipped" })
            : NotFound(new { id, error = "not awaiting review" });
    }

    [HttpGet("api/social/health")]
    public async Task<IActionResult> Health(CancellationToken ct)
        => Ok(await _health.GetAsync(ct));
}
