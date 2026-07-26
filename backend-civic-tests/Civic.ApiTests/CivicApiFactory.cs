using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Civic.API.Data;

namespace Civic.ApiTests;

public class CivicApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Port=5433;Database=civic_test;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // The verification-email gate is disabled in appsettings.json for the closed beta
        // (Auth:RequireVerifiedEmail=false), but the gate is read per-request, so force it
        // back on for the test suite. This keeps EmailVerificationGateTests exercising real
        // gate behavior; every other test mints verified tokens (JwtTestHelper default), so
        // the gate being on is transparent to them.
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:RequireVerifiedEmail"] = "true",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs captures the connection string at builder-config time, so an
            // AddInMemoryCollection override lands too late. Replace the DbContext options
            // registration directly instead.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CivicDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<CivicDbContext>(options =>
                options.UseNpgsql(TestConnectionString));

            // Keep the suite deterministic — no timer-driven content generators.
            TestHostConfig.DisableBackgroundGenerators(services);
        });
    }
}
