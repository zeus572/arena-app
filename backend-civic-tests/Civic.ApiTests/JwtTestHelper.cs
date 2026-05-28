using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Civic.ApiTests;

/// <summary>
/// Mints JWTs that match the *debate* backend's signing settings so the civic
/// backend treats them as authentic (shared Issuer/Audience/Secret).
/// </summary>
internal static class JwtTestHelper
{
    public const string Issuer = "arena-api";
    public const string Audience = "arena-app";
    public const string Secret = "PoliticalArenaDevSecretKeyThatIsAtLeast32Characters!!";

    public static string MintAccessToken(Guid userId, string email = "user@example.com", string plan = "Free")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("plan", plan),
            new("email_verified", "true"),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
