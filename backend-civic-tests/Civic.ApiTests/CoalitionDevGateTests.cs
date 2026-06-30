using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Civic.API.Data;
using Civic.API.Services.Coalition.Product;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// Boots the API in a NON-Development environment ("Staging") and proves the backend
/// itself blocks the dev-only affordances (seed / leagues-compose / agent-step / birth)
/// with 404 — independent of any frontend hiding — while normal gameplay (reads + acts)
/// still works. "Staging" avoids both the Development gate (so the gate is exercised) and
/// the Production branch (which would require Azure AD for the DB).
/// </summary>
[Collection("Database")]
public class CoalitionDevGateTests
{
    private sealed class StagingCivicApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CivicDbContext>));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddDbContext<CivicDbContext>(o => o.UseNpgsql(CivicApiFactory.TestConnectionString));
            });
        }
    }

    private static StringContent Json() => new("{}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task DevOnlyEndpoints_Return404_OutsideDevelopment_ButGameplayStillWorks()
    {
        using var factory = new StagingCivicApiFactory();
        await factory.WaitUntilReadyAsync();
        var client = factory.CreateClient();
        // A normal user act requires a verified, signed-in user; authenticate as one.
        // (The dev-only affordances below stay blocked regardless of auth.)
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestHelper.MintAccessToken(Guid.NewGuid()));

        // Dev-only affordances are blocked by the backend (not just hidden in the UI).
        (await client.PostAsync("/api/coalition/seed", Json())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync("/api/coalition/leagues/compose", Json())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/api/coalition/birth", new { briefingId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Normal gameplay is unaffected: reads work...
        var provisions = await client.GetFromJsonAsync<List<ProvisionSummaryDto>>("/api/coalition/provisions");
        provisions.Should().NotBeNull();

        if (provisions!.Count > 0)
        {
            var id = provisions[0].Id;
            // agent-step is dev-only -> blocked even on a real provision.
            (await client.PostAsync($"/api/coalition/provisions/{id}/agent-step", Json()))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            // ...but a normal user act still succeeds.
            (await client.PostAsJsonAsync($"/api/coalition/provisions/{id}/positions",
                new { stance = "for", intensity = "Medium", bucket = "left" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
