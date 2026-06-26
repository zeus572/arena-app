using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    private readonly ArenaDbContext _db;

    public JwtTokenService(IConfiguration config, ArenaDbContext db)
    {
        _config = config;
        _db = db;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("plan", user.Plan.ToString()),
            new("email_verified", user.EmailVerified.ToString().ToLower()),
        };

        if (user.DisplayName is not null)
            claims.Add(new Claim("display_name", user.DisplayName));

        var minutes = _config.GetValue("Jwt:AccessTokenMinutes", 60);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private const string MfaAudience = "arena-mfa";

    /// <summary>
    /// Short-lived token issued after a correct password when the user has MFA enabled,
    /// but before the second factor is presented. It is deliberately scoped to a
    /// distinct audience (<c>arena-mfa</c>) and carries <c>scope=mfa_pending</c>, so the
    /// API's Bearer middleware (which expects the <c>arena-app</c> audience) will NOT
    /// accept it as an access token — it is only valid at the MFA challenge endpoint.
    /// </summary>
    public string GenerateMfaPendingToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("scope", "mfa_pending"),
        };

        var minutes = _config.GetValue("Mfa:PendingTokenMinutes", 5);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: MfaAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Validate an MFA-pending token (signature, issuer, the <c>arena-mfa</c> audience,
    /// lifetime, and the <c>mfa_pending</c> scope) and return the user id it was issued
    /// for. Returns null on any failure.
    /// </summary>
    public Guid? ValidateMfaPendingToken(string token)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = MfaAudience,
            ValidateLifetime = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        try
        {
            // Disable inbound claim mapping so "sub" stays "sub" (matching how the app's
            // Bearer middleware is configured) instead of being remapped to the long
            // ClaimTypes.NameIdentifier URI — otherwise FindFirst("sub") would be null.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, parameters, out _);
            if (principal.FindFirst("scope")?.Value != "mfa_pending")
                return null;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> GenerateRefreshTokenAsync(User user)
    {
        var token = TokenHasher.NewToken();
        var hash = TokenHasher.Hash(token);

        var days = _config.GetValue("Jwt:RefreshTokenDays", 30);
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return token;
    }

    public async Task<User?> ValidateRefreshTokenAsync(string token)
    {
        var hash = TokenHasher.Hash(token);
        var rt = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null);

        if (rt is null || rt.ExpiresAt < DateTime.UtcNow)
            return null;

        // Rotate: revoke old token
        rt.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return rt.User;
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var hash = TokenHasher.Hash(token);
        var rt = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null);

        if (rt is not null)
        {
            rt.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
