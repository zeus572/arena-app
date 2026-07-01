using System.Linq;
using Civic.API.Services.Campaign;
using Civic.API.Services.Coalition.Product;
using Civic.API.Services.Generation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Civic.ApiTests;

internal static class TestHostConfig
{
    /// <summary>
    /// The background content generators (news ingest, civic content, campaign posts,
    /// coalition lifecycle) are nondeterministic in tests: they run on timers shortly
    /// after startup and mutate the very tables the integration tests assert on (e.g. a
    /// campaign-post generator firing mid-suite exhausts a candidate's post budget and
    /// breaks the cooldown test). Disable them so the suite is deterministic. The
    /// DatabaseInitializerService (migrate + seed + readiness) is intentionally kept.
    /// </summary>
    public static void DisableBackgroundGenerators(IServiceCollection services)
    {
        // Three are registered by type — remove their IHostedService descriptor.
        var byType = new[]
        {
            typeof(NewsIngestionService),
            typeof(CivicContentGenerationService),
            typeof(CoalitionLifecycleHostedService),
        };
        foreach (var implType in byType)
        {
            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == implType);
            if (descriptor is not null) services.Remove(descriptor);
        }

        // CampaignPostGenerationService is registered via a factory (no ImplementationType
        // to match on), but its loop is gated on CampaignOptions.Enabled — turn it off.
        // This only affects the DI-resolved hosted instance; tests that construct the
        // service directly use their own options and are unaffected.
        services.Configure<CampaignOptions>(o => o.Enabled = false);
    }
}
