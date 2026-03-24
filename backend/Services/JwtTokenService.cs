using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

    public async Task<string> GenerateRefreshTokenAsync(User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = ComputeHash(token);

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
        var hash = ComputeHash(token);
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
        var hash = ComputeHash(token);
        var rt = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null);

        if (rt is not null)
        {
            rt.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
