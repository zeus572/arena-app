using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class BillTimelineControllerTests
{
    private readonly HttpClient _client;

    public BillTimelineControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task List_ReturnsAllStepsInOrder()
    {
        var resp = await _client.GetAsync("/api/bill-timeline");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await resp.Content.ReadFromJsonAsync<List<BillTimelineStepDto>>();
        items.Should().NotBeNull();
        items!.Should().HaveCountGreaterOrEqualTo(5);
        items.Should().BeInAscendingOrder(s => s.Order);
        items.Should().ContainSingle(s => s.Status == "Current");
        items.Select(s => s.ExternalId).Should()
            .ContainInOrder("ts-001", "ts-002", "ts-003", "ts-004", "ts-005");
    }
}
