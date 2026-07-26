using System.Security.Claims;
using Civic.API.Services.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Unit coverage for the <c>Auth:RequireVerifiedEmail</c> toggle on the verified-email gate.
/// The integration <see cref="EmailVerificationGateTests"/> always run with the gate forced
/// on; these pin the toggle logic itself — including the relaxed closed-beta behavior
/// (gate off ⇒ signed-in but unverified users pass) that is the app's actual deployed
/// configuration while verification-email deliverability is being fixed.
/// </summary>
public class VerifiedEmailGateToggleTests
{
    private static AuthorizationHandlerContext Evaluate(bool emailVerified, IConfiguration config)
    {
        var handler = new VerifiedEmailHandler(config);
        var requirement = new VerifiedEmailRequirement();
        var identity = new ClaimsIdentity(
            new[] { new Claim("email_verified", emailVerified ? "true" : "false") },
            authenticationType: "test");
        var context = new AuthorizationHandlerContext(
            new[] { requirement }, new ClaimsPrincipal(identity), resource: null);
        handler.HandleAsync(context).GetAwaiter().GetResult();
        return context;
    }

    private static IConfiguration Config(string? requireVerifiedEmail)
    {
        var builder = new ConfigurationBuilder();
        if (requireVerifiedEmail is not null)
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RequireVerifiedEmail"] = requireVerifiedEmail,
            });
        return builder.Build();
    }

    [Fact]
    public void GateDisabled_UnverifiedUser_Passes()
    {
        // The closed-beta relaxation: with the flag off, an unverified account can act.
        var ctx = Evaluate(emailVerified: false, Config("false"));
        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void GateEnabled_UnverifiedUser_Fails()
    {
        var ctx = Evaluate(emailVerified: false, Config("true"));
        ctx.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public void GateEnabled_VerifiedUser_Passes()
    {
        var ctx = Evaluate(emailVerified: true, Config("true"));
        ctx.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void FlagUnset_DefaultsToSecure_UnverifiedUserFails()
    {
        // No override present → the code default (gate on) must hold, so re-enabling the
        // gate is as simple as removing the appsettings.json override.
        var ctx = Evaluate(emailVerified: false, Config(null));
        ctx.HasSucceeded.Should().BeFalse();
    }
}
