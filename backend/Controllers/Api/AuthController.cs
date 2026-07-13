using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Models.DTOs;
using Arena.API.Services;
using Arena.API.Services.Email;
using Arena.API.Services.Mfa;
using Microsoft.Extensions.Caching.Memory;

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
    private readonly TotpService _totp;
    private readonly MfaSecretProtector _mfaProtector;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthController> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    // COPPA: we do not knowingly create accounts for children under 13.
    private const int MinimumSignupAge = 13;

    public AuthController(
        ArenaDbContext db,
        JwtTokenService jwt,
        IConfiguration config,
        EmailPolicyService emailPolicy,
        AccountTokenService accountTokens,
        EmailDispatchService emailDispatch,
        TotpService totp,
        MfaSecretProtector mfaProtector,
        IMemoryCache cache,
        ILogger<AuthController> logger)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
        _emailPolicy = emailPolicy;
        _accountTokens = accountTokens;
        _emailDispatch = emailDispatch;
        _totp = totp;
        _mfaProtector = mfaProtector;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ValidateInviteCode(request.InviteCode))
            return BadRequest(new { error = "Invalid invite code." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        // COPPA age gate: require a plausible date of birth and reject under-13
        // before any account row is created. This endpoint is the single signup
        // path for both the Debate Arena and Civic frontends, so the gate here
        // protects both apps. (When OAuth signup is implemented — see the
        // GoogleLogin/MicrosoftLogin stubs below — it must apply the same gate.)
        if (request.DateOfBirth is not { } dob)
            return BadRequest(new { error = "Please enter your date of birth." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob > today || dob < today.AddYears(-120))
            return BadRequest(new { error = "Please enter a valid date of birth." });

        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--; // birthday hasn't occurred yet this year
        if (age < MinimumSignupAge)
            return BadRequest(new { error = $"You must be at least {MinimumSignupAge} years old to create an account." });

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
            DateOfBirth = dob,
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

        // Second factor: if the user has 2FA on, the password alone isn't enough.
        // A valid "remember this computer" token lets them skip it; otherwise we hand
        // back a short-lived MFA token and the client must complete /mfa/challenge.
        if (user.MfaEnabled)
        {
            if (!string.IsNullOrWhiteSpace(request.TrustedDeviceToken)
                && await IsTrustedDeviceValidAsync(user.Id, request.TrustedDeviceToken))
            {
                return await IssueSessionAsync(user, rememberDevice: false);
            }

            return Ok(new { mfaRequired = true, mfaToken = _jwt.GenerateMfaPendingToken(user) });
        }

        return await IssueSessionAsync(user, rememberDevice: false);
    }

    [HttpPost("mfa/challenge")]
    public async Task<IActionResult> MfaChallenge([FromBody] MfaChallengeRequest request)
    {
        var userId = _jwt.ValidateMfaPendingToken(request.MfaToken ?? string.Empty);
        if (userId is null)
            return Unauthorized(new { error = "Your sign-in session expired. Please log in again." });

        // Throttle code guessing: cap attempts per user within a short window.
        if (!RegisterMfaAttempt(userId.Value))
            return StatusCode(429, new { error = "Too many attempts. Please wait a moment and try again." });

        var user = await _db.Users.FindAsync(userId.Value);
        if (user is null || !user.MfaEnabled || user.TotpSecretEnc is null)
            return Unauthorized(new { error = "Invalid request." });

        if (!await VerifySecondFactorAsync(user, request.Code))
            return Unauthorized(new { error = "Invalid authentication code." });

        return await IssueSessionAsync(user, request.RememberDevice);
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
        {
            // Idempotency: the token is used or expired. If it belonged to a user who
            // is already verified, a prior request (an email-scanner prefetch that runs
            // JS, a double-click, or a browser refresh) already did the work — report
            // success rather than a confusing "expired" error. This is exactly the case
            // where the link "errored" but the profile shows Verified.
            var owner = await _accountTokens.PeekUserAsync(token, AccountTokenPurpose.EmailVerify);
            if (owner is { EmailVerified: true })
            {
                // Second hit on a link already consumed by an email-scanner prefetch,
                // a double-click, or a refresh. The account is verified — report success.
                _logger.LogInformation(
                    "verify-email: idempotent re-hit for already-verified user {UserId}; UA={UserAgent}",
                    owner.Id, Request.Headers.UserAgent.ToString());
                return Ok(new { status = "email verified" });
            }

            _logger.LogInformation("verify-email: rejected unknown/used/expired token; UA={UserAgent}",
                Request.Headers.UserAgent.ToString());
            return BadRequest(new { error = "This verification link is invalid or has expired." });
        }

        user.EmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("verify-email: verified user {UserId}; UA={UserAgent}",
            user.Id, Request.Headers.UserAgent.ToString());
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

        // Likewise revoke "remember this computer" bypass tokens — a reset should
        // force the second factor again on every device.
        var trusted = await _db.TrustedDevices
            .Where(d => d.UserId == user.Id && d.RevokedAt == null)
            .ToListAsync();
        foreach (var d in trusted) d.RevokedAt = DateTime.UtcNow;

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

    [Authorize]
    [HttpGet("mfa/status")]
    public async Task<IActionResult> MfaStatus()
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();

        var remaining = user.MfaEnabled
            ? await _db.MfaBackupCodes.CountAsync(c => c.UserId == user.Id && c.UsedAt == null)
            : 0;

        return Ok(new
        {
            enabled = user.MfaEnabled,
            enrolledAt = user.MfaEnrolledAt,
            backupCodesRemaining = remaining,
        });
    }

    [Authorize]
    [HttpPost("mfa/setup")]
    public async Task<IActionResult> MfaSetup()
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (user.MfaEnabled)
            return Conflict(new { error = "Two-factor authentication is already enabled." });

        // Generate a fresh secret and stash it (encrypted) as pending. MfaEnabled stays
        // false until the user proves they can produce a valid code via /mfa/enable.
        var secret = _totp.GenerateSecretBase32();
        user.TotpSecretEnc = _mfaProtector.Protect(secret);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            secret,
            otpauthUri = _totp.BuildOtpauthUri(user.Email, secret),
        });
    }

    [Authorize]
    [HttpPost("mfa/enable")]
    public async Task<IActionResult> MfaEnable([FromBody] MfaEnableRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (user.MfaEnabled)
            return Conflict(new { error = "Two-factor authentication is already enabled." });
        if (user.TotpSecretEnc is null)
            return BadRequest(new { error = "Start setup first." });

        var secret = _mfaProtector.Unprotect(user.TotpSecretEnc);
        if (!_totp.VerifyCode(secret, request.Code ?? string.Empty))
            return BadRequest(new { error = "That code is incorrect. Check your authenticator and try again." });

        user.MfaEnabled = true;
        user.MfaEnrolledAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        var codes = await RegenerateBackupCodesAsync(user);

        await _db.SaveChangesAsync();
        return Ok(new { status = "two-factor enabled", backupCodes = codes });
    }

    [Authorize]
    [HttpPost("mfa/disable")]
    public async Task<IActionResult> MfaDisable([FromBody] MfaDisableRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (!user.MfaEnabled)
            return Ok(new { status = "two-factor already disabled" });
        if (!VerifyPassword(user, request.Password))
            return Unauthorized(new { error = "Incorrect password." });

        user.MfaEnabled = false;
        user.TotpSecretEnc = null;
        user.MfaEnrolledAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await ClearBackupCodesAsync(user.Id);
        await RevokeTrustedDevicesAsync(user.Id);

        await _db.SaveChangesAsync();
        return Ok(new { status = "two-factor disabled" });
    }

    [Authorize]
    [HttpPost("mfa/backup-codes")]
    public async Task<IActionResult> MfaRegenerateBackupCodes([FromBody] MfaBackupCodesRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (!user.MfaEnabled)
            return BadRequest(new { error = "Enable two-factor authentication first." });
        if (!VerifyPassword(user, request.Password))
            return Unauthorized(new { error = "Incorrect password." });

        var codes = await RegenerateBackupCodesAsync(user);
        await _db.SaveChangesAsync();
        return Ok(new { backupCodes = codes });
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
        user.MfaEnabled,
    };

    // ---- MFA helpers -------------------------------------------------------

    private const int BackupCodeCount = 10;
    private const int MaxMfaAttempts = 5;
    private static readonly TimeSpan MfaAttemptWindow = TimeSpan.FromMinutes(15);

    private async Task<User?> CurrentUserAsync()
    {
        var sub = User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? await _db.Users.FindAsync(id) : null;
    }

    private bool VerifyPassword(User user, string? password)
    {
        if (user.PasswordHash is null || string.IsNullOrEmpty(password)) return false;
        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
               != PasswordVerificationResult.Failed;
    }

    /// <summary>Issue access + refresh tokens; optionally mint a 90-day trusted-device
    /// token so this client can skip the second factor next time.</summary>
    private async Task<IActionResult> IssueSessionAsync(User user, bool rememberDevice)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = await _jwt.GenerateRefreshTokenAsync(user);

        string? trustedDeviceToken = null;
        if (rememberDevice)
            trustedDeviceToken = await CreateTrustedDeviceAsync(user.Id);

        return Ok(new
        {
            accessToken,
            refreshToken,
            trustedDeviceToken,
            user = ProjectUser(user),
        });
    }

    /// <summary>Verify a second factor: a current TOTP code, or a single-use backup code.</summary>
    private async Task<bool> VerifySecondFactorAsync(User user, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        var secret = _mfaProtector.Unprotect(user.TotpSecretEnc!);
        if (_totp.VerifyCode(secret, code))
            return true;

        // Fall back to backup codes (normalized: strip dashes/space, upper-case).
        var normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length == 0) return false;

        var hash = TokenHasher.Hash(normalized);
        var match = await _db.MfaBackupCodes
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null);
        if (match is null) return false;

        match.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Replace the user's backup codes with a fresh set, returning the plaintext
    /// (shown once). Only the hashes are persisted. Does not call SaveChanges.</summary>
    private async Task<List<string>> RegenerateBackupCodesAsync(User user)
    {
        await ClearBackupCodesAsync(user.Id);

        var display = new List<string>(BackupCodeCount);
        for (var i = 0; i < BackupCodeCount; i++)
        {
            // 10 hex chars, shown grouped as XXXXX-XXXXX for readability.
            var raw = TokenHasher.NewToken(5).ToUpperInvariant();
            display.Add($"{raw[..5]}-{raw[5..]}");
            _db.MfaBackupCodes.Add(new MfaBackupCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CodeHash = TokenHasher.Hash(raw),
            });
        }
        return display;
    }

    private async Task ClearBackupCodesAsync(Guid userId)
    {
        var existing = await _db.MfaBackupCodes.Where(c => c.UserId == userId).ToListAsync();
        _db.MfaBackupCodes.RemoveRange(existing);
    }

    private async Task<string> CreateTrustedDeviceAsync(Guid userId)
    {
        var token = TokenHasher.NewToken();
        var days = _config.GetValue("Mfa:TrustedDeviceDays", 90);
        var ua = Request.Headers.UserAgent.ToString();
        _db.TrustedDevices.Add(new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = TokenHasher.Hash(token),
            Label = string.IsNullOrWhiteSpace(ua) ? null : ua[..Math.Min(ua.Length, 256)],
            ExpiresAt = DateTime.UtcNow.AddDays(days),
        });
        await _db.SaveChangesAsync();
        return token;
    }

    private async Task<bool> IsTrustedDeviceValidAsync(Guid userId, string token)
    {
        var hash = TokenHasher.Hash(token);
        var device = await _db.TrustedDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.TokenHash == hash && d.RevokedAt == null);
        return device is not null && device.ExpiresAt > DateTime.UtcNow;
    }

    private async Task RevokeTrustedDevicesAsync(Guid userId)
    {
        var devices = await _db.TrustedDevices
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .ToListAsync();
        foreach (var d in devices) d.RevokedAt = DateTime.UtcNow;
    }

    /// <summary>In-memory sliding cap on MFA challenge attempts per user, to slow code
    /// guessing. Returns false once the cap in the window is exceeded.</summary>
    private bool RegisterMfaAttempt(Guid userId)
    {
        var key = $"mfa:attempts:{userId}";
        var count = _cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = MfaAttemptWindow;
            return 0;
        });
        if (count >= MaxMfaAttempts) return false;
        _cache.Set(key, count + 1, MfaAttemptWindow);
        return true;
    }
}

public record RefreshRequest(string RefreshToken);
public record LinkAnonymousRequest(Guid AnonymousUserId);
