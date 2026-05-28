namespace Civic.API.Services;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string GetCurrentUserId();
}
