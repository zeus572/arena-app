using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly ArenaDbContext _db;
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(ArenaDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task<User> GetOrCreateUserAsync()
    {
        var header = _http.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();

        if (Guid.TryParse(header, out var userId))
        {
            var existing = await _db.Users.FindAsync(userId);
            if (existing is not null) return existing;
        }

        // Auto-create an anonymous user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"anon-{Guid.NewGuid():N}"[..16],
            Email = $"{Guid.NewGuid():N}@arena.local",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
