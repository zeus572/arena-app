using System.Net;
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
    // Mirrors the real v3/bill/{congress} list payload: numbers are STRINGS.
    private const string ListJson = """
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
        var http = new HttpClient(new StubHandler())
        {
            BaseAddress = new Uri("https://api.congress.gov/"),
        };
        var client = new CongressGovClient(http, "test-api-key", NullLogger<CongressGovClient>.Instance);

        var bills = await client.FetchRecentAsync(congress: 119, limit: 20);

        bills.Should().HaveCount(2);
        bills.Select(b => b.Number).Should().BeEquivalentTo(new[] { 144, 4941 });
        bills.Select(b => b.ExternalId).Should().Contain("hr-144-119");
        bills.Select(b => b.ExternalId).Should().Contain("s-4941-119");
    }

    /// <summary>Returns the detail payload for /hr/ and /s/ paths, else the list payload.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var isDetail = path.Contains("/hr/") || path.Contains("/s/");
            var json = isDetail ? DetailJson : ListJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
