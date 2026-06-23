using Civic.API.Models;
using FluentAssertions;
using Xunit;

namespace Civic.UnitTests;

public class ProfileDemographicsTests
{
    [Theory]
    [InlineData("98101", "WA")]   // Seattle
    [InlineData("99403", "WA")]   // top of WA range
    [InlineData("21201", "MD")]   // Baltimore
    [InlineData("20601", "MD")]   // bottom of MD range
    [InlineData("90001", "CA")]   // Los Angeles
    [InlineData("96161", "CA")]   // top of CA range
    [InlineData("98101-1234", "WA")] // ZIP+4 tolerated
    [InlineData(" 90001 ", "CA")]    // surrounding whitespace tolerated
    public void StateForZip_MapsSupportedStates(string zip, string expected)
    {
        Localities.StateForZip(zip).Should().Be(expected);
    }

    [Theory]
    [InlineData("10001")]  // New York — unsupported
    [InlineData("73301")]  // Texas — unsupported
    [InlineData("20500")]  // Washington DC (just below MD range)
    [InlineData("96200")]  // military/Pacific (just above CA range)
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ab")]     // not enough digits
    public void StateForZip_ReturnsNullForUnsupportedOrInvalid(string? zip)
    {
        Localities.StateForZip(zip).Should().BeNull();
    }

    [Theory]
    [InlineData("25_34", "25_34")]
    [InlineData("UNDER_18", "under_18")] // case-insensitive
    [InlineData(" 65_plus ", "65_plus")] // trimmed
    public void AgeRanges_TryNormalize_AcceptsSupportedKeys(string raw, string expected)
    {
        AgeRanges.TryNormalize(raw, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AgeRanges_TryNormalize_TreatsEmptyAsUnset(string? raw)
    {
        AgeRanges.TryNormalize(raw, out var normalized).Should().BeTrue();
        normalized.Should().BeNull();
    }

    [Theory]
    [InlineData("99")]
    [InlineData("teenager")]
    [InlineData("18-24")] // wrong separator (keys use underscore)
    public void AgeRanges_TryNormalize_RejectsUnknownValues(string raw)
    {
        AgeRanges.TryNormalize(raw, out var normalized).Should().BeFalse();
        normalized.Should().BeNull();
    }
}
