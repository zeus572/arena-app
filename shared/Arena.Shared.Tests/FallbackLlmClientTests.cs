using Arena.Shared.Llm;
using FluentAssertions;
using Xunit;

namespace Arena.Shared.Tests;

public class FallbackLlmClientTests
{
    private record Demo(string Source, int Rank);

    /// <summary>Configurable stub ILlmClient that returns a value or throws, and counts calls.</summary>
    private sealed class FakeLlm : ILlmClient
    {
        private readonly Func<Task<object>> _behavior;
        public int Calls { get; private set; }

        private FakeLlm(Func<Task<object>> behavior) => _behavior = behavior;

        public static FakeLlm Returns(object value) => new(() => Task.FromResult(value));
        public static FakeLlm Throws(Exception ex) => new(() => Task.FromException<object>(ex));

        public async Task<T> GenerateStructuredAsync<T>(
            string systemPrompt, string userPrompt,
            LlmModelTier tier = LlmModelTier.Sonnet, int? maxTokens = null, CancellationToken ct = default)
        {
            Calls++;
            return (T)await _behavior();
        }
    }

    [Fact]
    public async Task PrimarySucceeds_BackupNeverCalled()
    {
        var primary = FakeLlm.Returns(new Demo("primary", 1));
        var backup = FakeLlm.Returns(new Demo("backup", 2));
        var client = new FallbackLlmClient(primary, backup);

        var result = await client.GenerateStructuredAsync<Demo>("s", "u");

        result.Source.Should().Be("primary");
        backup.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(LlmFailureKind.CallFailed)]
    [InlineData(LlmFailureKind.BadResponse)]
    [InlineData(LlmFailureKind.Unavailable)]
    public async Task PrimaryLlmException_FallsBackToBackup(LlmFailureKind kind)
    {
        var primary = FakeLlm.Throws(new LlmException("primary down", kind: kind));
        var backup = FakeLlm.Returns(new Demo("backup", 2));
        var client = new FallbackLlmClient(primary, backup);

        var result = await client.GenerateStructuredAsync<Demo>("s", "u");

        result.Source.Should().Be("backup");
        backup.Calls.Should().Be(1);
    }

    [Fact]
    public async Task PrimaryTransportError_FallsBackToBackup()
    {
        var primary = FakeLlm.Throws(new HttpRequestException("connection reset"));
        var backup = FakeLlm.Returns(new Demo("backup", 2));
        var client = new FallbackLlmClient(primary, backup);

        var result = await client.GenerateStructuredAsync<Demo>("s", "u");

        result.Source.Should().Be("backup");
    }

    [Fact]
    public async Task PrimaryTimeout_FallsBackToBackup()
    {
        // HttpClient.Timeout surfaces as TaskCanceledException with NO caller cancellation.
        var primary = FakeLlm.Throws(new TaskCanceledException("timed out"));
        var backup = FakeLlm.Returns(new Demo("backup", 2));
        var client = new FallbackLlmClient(primary, backup);

        var result = await client.GenerateStructuredAsync<Demo>("s", "u", ct: CancellationToken.None);

        result.Source.Should().Be("backup");
    }

    [Fact]
    public async Task CallerCancellation_Propagates_BackupNeverCalled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var primary = FakeLlm.Throws(new OperationCanceledException(cts.Token));
        var backup = FakeLlm.Returns(new Demo("backup", 2));
        var client = new FallbackLlmClient(primary, backup);

        var act = async () => await client.GenerateStructuredAsync<Demo>("s", "u", ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        backup.Calls.Should().Be(0, "a caller-cancelled request must not silently retry on the backup");
    }

    [Fact]
    public async Task BothFail_SurfacesMoreSevereKind_CallFailedWins()
    {
        // Primary genuinely failed (out of credits); backup is just unconfigured (Unavailable).
        // Callers must still see CallFailed so batch jobs bail instead of persisting dead data.
        var primary = FakeLlm.Throws(new LlmException("out of credits", kind: LlmFailureKind.CallFailed));
        var backup = FakeLlm.Throws(new LlmException("no key", kind: LlmFailureKind.Unavailable));
        var client = new FallbackLlmClient(primary, backup);

        var act = async () => await client.GenerateStructuredAsync<Demo>("s", "u");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.Kind.Should().Be(LlmFailureKind.CallFailed);
    }

    [Fact]
    public async Task BothUnavailable_SurfacesUnavailable_CallersFallBackToHeuristics()
    {
        var primary = FakeLlm.Throws(new LlmException("claude off", kind: LlmFailureKind.Unavailable));
        var backup = FakeLlm.Throws(new LlmException("gpt off", kind: LlmFailureKind.Unavailable));
        var client = new FallbackLlmClient(primary, backup);

        var act = async () => await client.GenerateStructuredAsync<Demo>("s", "u");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.Kind.Should().Be(LlmFailureKind.Unavailable);
    }

    [Fact]
    public async Task BothFail_PrimaryTransport_BackupBadResponse_SurfacesCallFailed()
    {
        // A transport failure is outage-class (CallFailed severity) and outranks the backup's
        // BadResponse, so the surfaced kind is CallFailed.
        var primary = FakeLlm.Throws(new HttpRequestException("connection reset"));
        var backup = FakeLlm.Throws(new LlmException("garbage", kind: LlmFailureKind.BadResponse));
        var client = new FallbackLlmClient(primary, backup);

        var act = async () => await client.GenerateStructuredAsync<Demo>("s", "u");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.Kind.Should().Be(LlmFailureKind.CallFailed);
    }
}
