namespace Civic.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool IsAuthenticated =>
        _http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string GetCurrentUserId()
    {
        var sub = _http.HttpContext?.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return sub;

        var header = _http.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header)) return header;

        return "anonymous";
    }
}
