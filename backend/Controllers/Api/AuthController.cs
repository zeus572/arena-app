using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthController(ArenaDbContext db, JwtTokenService jwt, IConfiguration config)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var requiredCode = _config["Auth:InviteCode"];
        if (!string.IsNullOrEmpty(requiredCode) &&
            !string.Equals(request.InviteCode?.Trim(), requiredCode, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid invite code." });
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { error = "Valid email is required." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var emailLower = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == emailLower && !u.IsAnonymous);
        if (exists)
            return Conflict(new { error = "An account with this email already exists." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailLower,
            Username = request.DisplayName?.Trim() ?? emailLower.Split('@')[0],
            DisplayName = request.DisplayName?.Trim(),
            AuthProvider = "local",
            IsAnonymous = false,
            EmailVerified = false,
            EmailVerifyToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Plan = UserPlan.Free,
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = ProjectUser(user),
            emailVerifyToken = user.EmailVerifyToken, // MVP: return token directly
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == emailLower && u.AuthProvider == "local" && !u.IsAnonymous);

        if (user is null || user.PasswordHash is null)
            return Unauthorized(new { error = "Invalid email or password." });

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid email or password." });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = ProjectUser(user),
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var user = await _jwt.ValidateRefreshTokenAsync(request.RefreshToken);
        if (user is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user);

        return Ok(new { accessToken, refreshToken });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _jwt.RevokeRefreshTokenAsync(request.RefreshToken);

        return Ok(new { status = "logged out" });
    }

    [Authorize]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification()
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (user.EmailVerified)
            return Ok(new { status = "already verified" });

        if (string.IsNullOrEmpty(user.EmailVerifyToken))
        {
            user.EmailVerifyToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await _db.SaveChangesAsync();
        }

        // MVP: return token directly (no actual email sent)
        return Ok(new { emailVerifyToken = user.EmailVerifyToken });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Token is required." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailVerifyToken == token);
        if (user is null)
            return NotFound(new { error = "Invalid verification token." });

        user.EmailVerified = true;
        user.EmailVerifyToken = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { status = "email verified" });
    }

    [Authorize]
    [HttpPost("link-anonymous")]
    public async Task<IActionResult> LinkAnonymous([FromBody] LinkAnonymousRequest request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var authUser = await _db.Users.FindAsync(userId);
        if (authUser is null) return NotFound();

        var anonUser = await _db.Users.FindAsync(request.AnonymousUserId);
        if (anonUser is null || !anonUser.IsAnonymous)
            return BadRequest(new { error = "Invalid anonymous user." });

        // Transfer votes
        var votes = await _db.Votes.Where(v => v.UserId == anonUser.Id).ToListAsync();
        foreach (var v in votes) v.UserId = authUser.Id;

        // Transfer reactions
        var reactions = await _db.Reactions.Where(r => r.UserId == anonUser.Id).ToListAsync();
        foreach (var r in reactions) r.UserId = authUser.Id;

        await _db.SaveChangesAsync();

        return Ok(new { transferred = new { votes = votes.Count, reactions = reactions.Count } });
    }

    private static object ProjectUser(User user) => new
    {
        user.Id,
        user.Email,
        user.DisplayName,
        user.AvatarUrl,
        user.PoliticalLeaning,
        Plan = user.Plan.ToString(),
        user.EmailVerified,
    };
}

public record RefreshRequest(string RefreshToken);
public record LinkAnonymousRequest(Guid AnonymousUserId);
