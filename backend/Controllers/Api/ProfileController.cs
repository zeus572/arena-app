using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Arena.API.Data;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ArenaDbContext _db;

    public ProfileController(ArenaDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.PoliticalLeaning,
            Plan = user.Plan.ToString(),
            user.EmailVerified,
            user.AuthProvider,
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName.Trim();
        if (request.PoliticalLeaning is not null)
            user.PoliticalLeaning = request.PoliticalLeaning.Trim();
        if (request.AvatarUrl is not null)
            user.AvatarUrl = request.AvatarUrl.Trim();

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.PoliticalLeaning,
            Plan = user.Plan.ToString(),
            user.EmailVerified,
        });
    }
}

public record UpdateProfileRequest(string? DisplayName, string? PoliticalLeaning, string? AvatarUrl);
