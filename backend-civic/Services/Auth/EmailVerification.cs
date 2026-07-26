using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;

namespace Civic.API.Services.Auth;

/// <summary>
/// Authorization requirement satisfied only when the caller's JWT carries
/// <c>email_verified == "true"</c>. The claim is minted by the debate (Arena) backend
/// — which owns auth for both apps — from <c>User.EmailVerified</c>, and refreshes on
/// token rotation, so verifying an email propagates to the next refreshed access token.
///
/// Used by the <c>"VerifiedEmail"</c> policy to keep unverified (often throwaway) accounts
/// from performing account-bound write/participation actions (leagues, campaign manager,
/// coalition acts, petitions) while still allowing them to read/browse and finish onboarding.
/// </summary>
public sealed class VerifiedEmailRequirement : IAuthorizationRequirement
{
}

public sealed class VerifiedEmailHandler : AuthorizationHandler<VerifiedEmailRequirement>
{
    private readonly IConfiguration _config;

    public VerifiedEmailHandler(IConfiguration config) => _config = config;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, VerifiedEmailRequirement requirement)
    {
        // Closed invite-only beta (2026-07): the email-verification gate is temporarily
        // relaxed while verification-email deliverability is fixed, so signed-in users can
        // participate without verifying. `Auth:RequireVerifiedEmail` is set to false in
        // appsettings.json; delete that override (or set it true) to re-enable the gate.
        // Read per-request so flipping the setting takes effect without a code change, and
        // so the gate stays fully covered by EmailVerificationGateTests (which force it on).
        var gateOn = _config.GetValue("Auth:RequireVerifiedEmail", true);

        if (!gateOn || context.User.FindFirst("email_verified")?.Value == "true")
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Shapes the response when the <c>"VerifiedEmail"</c> policy fails for an already
/// authenticated user: instead of an empty 403 we return a machine-readable body
/// (<c>code: "email_unverified"</c>) so the frontend can show a "verify your email"
/// prompt rather than a generic error. Every other authorization outcome — including
/// the 401 challenge for unauthenticated callers — is delegated to the framework default,
/// so this handler is inert for all other policies.
/// </summary>
public sealed class CivicAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var unverified = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<VerifiedEmailRequirement>().Any() == true;

        if (unverified)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Please verify your email to participate.",
                code = "email_unverified",
            });
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
