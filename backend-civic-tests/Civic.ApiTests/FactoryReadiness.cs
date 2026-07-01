using Civic.API.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Civic.ApiTests;

/// <summary>
/// Civic now migrates + seeds in the background (DatabaseInitializerService) instead of
/// inline before app.Run(), and a readiness gate returns 503 until that finishes. Tests
/// must therefore wait for the host to become Ready before issuing requests, otherwise
/// they race the gate. This await is deterministic: it completes exactly when init does.
/// </summary>
internal static class FactoryReadiness
{
    public static async Task WaitUntilReadyAsync(this WebApplicationFactory<Program> factory)
    {
        // Touch CreateClient first so the host (and its hosted services) actually start.
        using var _ = factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var readiness = factory.Services.GetRequiredService<StartupReadiness>();
        await readiness.WaitUntilReadyAsync(cts.Token);
    }
}
