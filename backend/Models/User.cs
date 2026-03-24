namespace Arena.API.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public string? PoliticalLeaning { get; set; }
    public string? AvatarUrl { get; set; }
    public UserPlan Plan { get; set; } = UserPlan.Free;
    public string? AuthProvider { get; set; }
    public string? ExternalId { get; set; }
    public bool EmailVerified { get; set; }
    public string? EmailVerifyToken { get; set; }
    public bool IsAnonymous { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
