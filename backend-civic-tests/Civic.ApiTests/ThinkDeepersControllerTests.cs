using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class ThinkDeepersControllerTests
{
    private readonly HttpClient _client;

    public ThinkDeepersControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task GetBySlug_ReturnsThinkDeeper()
    {
        var resp = await _client.GetAsync("/api/think-deepers/student-data-privacy");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await resp.Content.ReadFromJsonAsync<ThinkDeeperDto>();
        dto.Should().NotBeNull();
        dto!.Issue.Should().Contain("student data privacy");
        dto.Values.Should().Contain("Privacy");
        dto.StrongestArgumentA.Should().NotBeNullOrWhiteSpace();
        dto.StrongestArgumentB.Should().NotBeNullOrWhiteSpace();
        dto.WhatWouldChangeYourMind.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var resp = await _client.GetAsync("/api/think-deepers/nope");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
