using Arena.API.Services.Email;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.UnitTests;

public class EmailPolicyServiceTests
{
    // Use the Development environment so the DNS MX check is skipped — these tests
    // exercise format + disposable logic without touching the network.
    private static EmailPolicyService CreateService(params string[] extraDisposable)
    {
        var options = Options.Create(new EmailOptions
        {
            CheckMx = true,
            DisposableDomains = extraDisposable.ToList(),
        });
        return new EmailPolicyService(
            options,
            new MemoryCache(new MemoryCacheOptions()),
            new FakeEnv("Development"),
            NullLogger<EmailPolicyService>.Instance);
    }

    [Theory]
    [InlineData("  Foo.Bar@Example.COM ", "foo.bar@example.com")]
    [InlineData("user+tag@gmail.com", "user+tag@gmail.com")]
    public async Task Accepts_and_normalizes_valid_addresses(string input, string expected)
    {
        var result = await CreateService().ValidateAsync(input);

        result.Accepted.Should().BeTrue();
        result.Normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@@b.com")]
    [InlineData("no domain@")]
    [InlineData("")]
    [InlineData("Display Name <a@b.com>")]
    public async Task Rejects_malformed_addresses(string input)
    {
        var result = await CreateService().ValidateAsync(input);

        result.Accepted.Should().BeFalse();
        result.Reason.Should().Be(EmailRejectReason.Malformed);
    }

    [Fact]
    public async Task Rejects_bundled_disposable_domain()
    {
        var result = await CreateService().ValidateAsync("throwaway@mailinator.com");

        result.Accepted.Should().BeFalse();
        result.Reason.Should().Be(EmailRejectReason.Disposable);
    }

    [Fact]
    public async Task Rejects_config_supplied_disposable_domain()
    {
        var result = await CreateService("spammy.example").ValidateAsync("x@spammy.example");

        result.Accepted.Should().BeFalse();
        result.Reason.Should().Be(EmailRejectReason.Disposable);
    }

    [Fact]
    public void Normalize_returns_null_for_garbage()
    {
        EmailPolicyService.Normalize("nonsense").Should().BeNull();
        EmailPolicyService.Normalize(null).Should().BeNull();
    }

    private sealed class FakeEnv : IHostEnvironment
    {
        public FakeEnv(string env) => EnvironmentName = env;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Arena.API";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
