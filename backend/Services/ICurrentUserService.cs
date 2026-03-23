using Arena.API.Models;

namespace Arena.API.Services;

public interface ICurrentUserService
{
    Task<User> GetOrCreateUserAsync();
}
