namespace Arena.API.Services.Email;

/// <summary>
/// Constant-time comparison for the shared secret guarding the operator email
/// endpoints (<c>/api/admin/email-smoke</c>, <c>/api/admin/email/resend-verification</c>).
///
/// The implementation moved to <see cref="Arena.Shared.Security.SharedSecret"/> so the Civic
/// backend's operator endpoints compare secrets exactly the same way instead of growing a
/// second copy; this stays as the name Arena callers already use.
/// </summary>
public static class SharedSecret
{
    public static bool Matches(string? provided, string? expected) =>
        Arena.Shared.Security.SharedSecret.Matches(provided, expected);
}
