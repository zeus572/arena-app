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
        => Mint(userId, email, plan, Secret, DateTime.UtcNow.AddMinutes(30));

    /// <summary>A correctly-shaped token signed with the WRONG secret — models the
    /// prod failure where civic's signing key doesn't match the debate backend's.</summary>
    public static string MintWronglySignedToken(Guid userId)
        => Mint(userId, "user@example.com", "Free",
            "TotallyDifferentSecretThatIsAlsoAtLeast32Characters!!", DateTime.UtcNow.AddMinutes(30));

    /// <summary>A properly-signed token whose lifetime has already elapsed.</summary>
    public static string MintExpiredToken(Guid userId)
        => Mint(userId, "user@example.com", "Free", Secret, DateTime.UtcNow.AddMinutes(-5));

    private static string Mint(Guid userId, string email, string plan, string secret, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
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
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
