using System.Net;
using System.Reflection;
using System.Text;
using Civic.API.Services.Bills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Civic.UnitTests;

/// <summary>
/// Regression coverage for the live Congress.gov JSON shape. The real API
/// serializes bill "number" as a JSON string ("144"), not a number — which
/// System.Text.Json refuses to bind to <c>int?</c> unless configured to allow
/// reading numbers from strings. Before that fix, deserialization threw on the
/// first bill and <see cref="CongressGovClient.FetchRecentAsync"/> silently
/// swallowed it, so every live fetch returned zero bills.
/// </summary>
public class CongressGovClientTests
{
    // Minimal synthetic list payload: numbers are STRINGS, like the real API.
    private const string SyntheticListJson = """
    {
      "bills": [
        {
          "congress": 119,
          "number": "144",
          "type": "HR",
          "title": "Tennessee Valley Authority Salary Transparency Act",
          "url": "https://api.congress.gov/v3/bill/119/hr/144?format=json",
          "latestAction": { "actionDate": "2025-01-16", "text": "Referred to committee." }
        },
        {
          "congress": 119,
          "number": "4941",
          "type": "S",
          "title": "Modernizing Opioid Treatment Access Act",
          "url": "https://api.congress.gov/v3/bill/119/s/4941?format=json",
          "latestAction": { "actionDate": "2026-06-24", "text": "Read twice and referred." }
        }
      ],
      "pagination": { "count": 2 }
    }
    """;

    private const string DetailJson = """
    {
      "bill": {
        "title": "Tennessee Valley Authority Salary Transparency Act",
        "introducedDate": "2025-01-10",
        "sponsors": [ { "fullName": "Rep. Diana Harshbarger", "party": "R" } ]
      }
    }
    """;

    [Fact]
    public async Task FetchRecentAsync_ParsesBills_WhenNumberIsJsonString()
    {
        var client = ClientFor(SyntheticListJson);

        var bills = await client.FetchRecentAsync(congress: 119, limit: 20);

        bills.Should().HaveCount(2);
        bills.Select(b => b.Number).Should().BeEquivalentTo(new[] { 144, 4941 });
        bills.Select(b => b.ExternalId).Should().Contain("hr-144-119");
        bills.Select(b => b.ExternalId).Should().Contain("s-4941-119");
    }

    /// <summary>
    /// Foolproofing against reality: an unmodified capture of a real
    /// <c>GET /v3/bill/119?sort=updateDate+desc&amp;limit=20</c> response
    /// (embedded verbatim as <c>TestData/congress_bill_list_119.json</c>).
    /// If Congress.gov's schema drifts in a way the DTOs can't bind, this
    /// breaks — instead of silently ingesting nothing in prod.
    /// </summary>
    [Fact]
    public async Task FetchRecentAsync_ParsesEntireRealCongressGovResponse()
    {
        var realJson = LoadFixture("congress_bill_list_119.json");
        var client = ClientFor(realJson);

        var bills = await client.FetchRecentAsync(congress: 119, limit: 20);

        bills.Should().HaveCount(20, "every bill in the real response must deserialize");
        bills.Should().OnlyContain(b => b.Number > 0);
        bills.Should().OnlyContain(b => !string.IsNullOrWhiteSpace(b.Title));
        // Spot-check a few real bills across chambers.
        var ids = bills.Select(b => b.ExternalId).ToList();
        ids.Should().Contain("hr-4541-119");   // EARLY Act Reauthorization of 2025
        ids.Should().Contain("s-4941-119");    // Modernizing Opioid Treatment Access Act 2.0
        ids.Should().Contain("hr-9594-119");   // No PFAS in Cosmetics Act
    }

    private static CongressGovClient ClientFor(string listJson)
    {
        var http = new HttpClient(new StubHandler(listJson))
        {
            BaseAddress = new Uri("https://api.congress.gov/"),
        };
        return new CongressGovClient(http, "test-api-key", NullLogger<CongressGovClient>.Instance);
    }

    private static string LoadFixture(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Serves <paramref name="listJson"/> for the list endpoint and a minimal
    /// detail payload for /hr/ and /s/ detail lookups.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _listJson;

        public StubHandler(string listJson) => _listJson = listJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var isDetail = path.Contains("/hr/") || path.Contains("/s/");
            var json = isDetail ? DetailJson : _listJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
