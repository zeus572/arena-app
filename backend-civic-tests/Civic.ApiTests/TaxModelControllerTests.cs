using System.Net;
using System.Net.Http.Json;
using Civic.API.Models.DTOs;
using FluentAssertions;
using Xunit;

namespace Civic.ApiTests;

[Collection("Database")]
public class TaxModelControllerTests
{
    private readonly HttpClient _client;

    public TaxModelControllerTests(DatabaseFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task States_ReturnsAllFiftyVerifiedStates()
    {
        var states = await _client.GetFromJsonAsync<List<TaxStateSummaryDto>>("/api/tax-model/states");

        states.Should().NotBeNull();
        // The catalog grew from the original 8 spotlight states to all 50 (the remaining
        // 42 verified against Tax Foundation 2025 — see StateProfiles.cs).
        states!.Should().HaveCount(50);
        // The 8 fully-verified spotlight states must still be present.
        states.Select(s => s.Code).Should().Contain(new[] { "CA", "NY", "TX", "FL", "WA", "CO", "PA", "IL" });
        states.First(s => s.Code == "TX").IncomeSummary.Should().Be("None");
        states.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Notes));
    }

    [Fact]
    public async Task Compute_TexasSingle100k_MatchesGoldenSpotCheck()
    {
        var dto = await _client.GetFromJsonAsync<TaxComputeDto>(
            "/api/tax-model/compute?income=100000&filing=single&state=TX");

        dto.Should().NotBeNull();
        dto!.State.Should().Be("TX");
        dto.Filing.Should().Be("single");

        // §6 federal spot-check (FICA + correct income tax per §3 brackets).
        dto.Federal.SocialSecurity.Should().BeApproximately(6_200.00, 0.01);
        dto.Federal.Medicare.Should().BeApproximately(1_450.00, 0.01);
        dto.Federal.IncomeTax.Should().BeApproximately(13_449.00, 0.01);

        // §6 state spot-check for TX.
        dto.StateLocal.IncomeTax.Should().Be(0);
        dto.StateLocal.SalesTax.Should().BeApproximately(3_280.00, 0.01);
        dto.StateLocal.PropertyTax.Should().BeApproximately(5_056.00, 0.01);
        dto.StateLocal.Total.Should().BeApproximately(8_336.00, 0.01);

        (dto.Combined.FederalShare + dto.Combined.StateShare).Should().BeApproximately(1.0, 1e-9);
        dto.Combined.Total.Should().BeApproximately(dto.Federal.Total + dto.StateLocal.Total, 0.01);
    }

    [Fact]
    public async Task Compute_UnknownState_ReturnsValidationProblem()
    {
        var resp = await _client.GetAsync("/api/tax-model/compute?income=100000&filing=single&state=ZZ");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Compute_NegativeIncome_ReturnsValidationProblem()
    {
        var resp = await _client.GetAsync("/api/tax-model/compute?income=-5&filing=single&state=CA");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ladder_ReturnsSixPresetRows_WithRisingFederalShare()
    {
        var dto = await _client.GetFromJsonAsync<TaxLadderDto>(
            "/api/tax-model/ladder?filing=single&state=TX");

        dto.Should().NotBeNull();
        dto!.Rows.Should().HaveCount(6);
        dto.Rows.Select(r => r.Income).Should()
            .ContainInOrder(30_000, 60_000, 100_000, 175_000, 350_000, 750_000);

        // Federal share rises with income (steeply progressive federal income tax).
        dto.Rows.First().FederalShare.Should().BeLessThan(dto.Rows.Last().FederalShare);
        dto.Rows.Should().OnlyContain(r =>
            Math.Abs(r.FederalShare + r.StateShare - 1.0) < 1e-9);
    }

    [Fact]
    public async Task Compute_MfjAlias_IsAccepted()
    {
        var dto = await _client.GetFromJsonAsync<TaxComputeDto>(
            "/api/tax-model/compute?income=300000&filing=married&state=CA");

        dto.Should().NotBeNull();
        dto!.Filing.Should().Be("mfj");
        // MFJ additional-Medicare threshold is $250k: (300k − 250k) × 0.9% = $450.
        dto.Federal.AddlMedicare.Should().BeApproximately(450.00, 0.01);
    }
}
