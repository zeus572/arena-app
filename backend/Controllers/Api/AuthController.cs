using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;
using Arena.API.Services.Email;

namespace Arena.API.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ArenaDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IConfiguration _config;
    private readonly EmailPolicyService _emailPolicy;
    private readonly AccountTokenService _accountTokens;
    private readonly EmailDispatchService _emailDispatch;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthController(
        ArenaDbContext db,
        JwtTokenService jwt,
        IConfiguration config,
        EmailPolicyService emailPolicy,
        AccountTokenService accountTokens,
        EmailDispatchService emailDispatch)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
        _emailPolicy = emailPolicy;
        _accountTokens = accountTokens;
        _emailDispatch = emailDispatch;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ValidateInviteCode(request.InviteCode))
            return BadRequest(new { error = "Invalid invite code." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var check = await _emailPolicy.ValidateAsync(request.Email);
        if (!check.Accepted)
            return BadRequest(new { error = check.Message });

        var emailLower = check.Normalized;
        var exists = await _db.Users.AnyAsync(u => u.Email == emailLower && !u.IsAnonymous);
        if (exists)
            return Conflict(new { error = "An account with this email already exists." });

        var baseUsername = request.DisplayName?.Trim() is { Length: > 0 } dn ? dn : emailLower.Split('@')[0];
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailLower,
            Username = await EnsureUniqueUsernameAsync(baseUsername),
            DisplayName = request.DisplayName?.Trim(),
            AuthProvider = "local",
            IsAnonymous = false,
            EmailVerified = false,
            Plan = UserPlan.Free,
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique-index violation under a concurrent signup race: another request
            // inserted the same email between our check above and this save. Surface
            // the same clean 409 rather than a 500.
            return Conflict(new { error = "An account with this email already exists." });
        }

        // Send a real verification email (token lives only in the link, never the response).
        var verifyToken = await _accountTokens.IssueAsync(user, AccountTokenPurpose.EmailVerify);
        await _emailDispatch.SendAccountEmailAsync(
            user, AccountTokenPurpose.EmailVerify, verifyToken, request.App ?? "arena", ClientIp());

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = ProjectUser(user),
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
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest? request)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        if (user.EmailVerified)
            return Ok(new { status = "already verified" });

        var token = await _accountTokens.IssueAsync(user, AccountTokenPurpose.EmailVerify);
        var result = await _emailDispatch.SendAccountEmailAsync(
            user, AccountTokenPurpose.EmailVerify, token, request?.App ?? "arena", ClientIp());

        if (result == DispatchResult.RateLimited)
            return StatusCode(429, new { error = "Too many requests. Please try again later." });

        return Ok(new { status = "verification email sent" });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        var user = await _accountTokens.ConsumeAsync(token, AccountTokenPurpose.EmailVerify);
        if (user is null)
            return BadRequest(new { error = "This verification link is invalid or has expired." });

        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { status = "email verified" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Always return the same 200 regardless of whether the account exists —
        // never reveal which addresses are registered (no user enumeration).
        var emailLower = EmailPolicyService.Normalize(request.Email);
        if (emailLower is not null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Email == emailLower && u.AuthProvider == "local" && !u.IsAnonymous);
            if (user is not null)
            {
                var token = await _accountTokens.IssueAsync(user, AccountTokenPurpose.PasswordReset);
                await _emailDispatch.SendAccountEmailAsync(
                    user, AccountTokenPurpose.PasswordReset, token, request.App ?? "arena", ClientIp());
            }
        }

        return Ok(new { status = "If an account exists for that email, a reset link is on its way." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var user = await _accountTokens.ConsumeAsync(request.Token, AccountTokenPurpose.PasswordReset);
        if (user is null)
            return BadRequest(new { error = "This reset link is invalid or has expired." });

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Security: revoke every outstanding refresh token so a thief who already
        // had a session is forced back to login with the new password.
        var sessions = await _db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.RevokedAt == null)
            .ToListAsync();
        foreach (var s in sessions) s.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { status = "password reset" });
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

    // OAuth endpoints — when implementing Google/Microsoft callbacks:
    // 1. Extract email and external ID from OAuth claims
    // 2. Check if user exists by (AuthProvider, ExternalId)
    // 3. If existing user → allow login (no invite code needed)
    // 4. If new user → validate invite_code from query param → reject if invalid
    // The invite_code is passed via query param: /api/auth/google?invite_code=ARENA7X

    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string? invite_code)
    {
        // TODO: Implement Google OAuth challenge
        // For now, return not implemented
        return BadRequest(new { error = "Google OAuth is not yet configured. Please register with email and password." });
    }

    [HttpGet("microsoft")]
    public IActionResult MicrosoftLogin([FromQuery] string? invite_code)
    {
        // TODO: Implement Microsoft OAuth challenge
        return BadRequest(new { error = "Microsoft OAuth is not yet configured. Please register with email and password." });
    }

    /// <summary>
    /// Validates the invite code. Returns true if valid or if no invite code is configured.
    /// Used by both register and OAuth callback endpoints.
    /// </summary>
    /// <summary>Pick a username that doesn't collide with the unique index. Two
    /// distinct emails can derive the same base (john@a.com / john@b.com → "john"),
    /// so append a short random suffix until it's free.</summary>
    private async Task<string> EnsureUniqueUsernameAsync(string baseUsername)
    {
        var candidate = baseUsername;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!await _db.Users.AnyAsync(u => u.Username == candidate))
                return candidate;
            candidate = $"{baseUsername}-{TokenHasher.NewToken(2)}";
        }
        // Extremely unlikely fallback: guarantee uniqueness.
        return $"{baseUsername}-{Guid.NewGuid():N}";
    }

    /// <summary>Best-effort client IP for rate limiting (honors a reverse-proxy
    /// X-Forwarded-For when present).</summary>
    private string? ClientIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd))
            return fwd.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private bool ValidateInviteCode(string? inviteCode)
    {
        var requiredCode = _config["Auth:InviteCode"];
        if (string.IsNullOrEmpty(requiredCode)) return true;
        return string.Equals(inviteCode?.Trim(), requiredCode, StringComparison.OrdinalIgnoreCase);
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
