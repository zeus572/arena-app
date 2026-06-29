using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arena.Shared.Social;

/// <summary>
/// The single integration point between the Bot Heartbeat and the SocialPublisher
/// (SocialPublisher_Spec §3, §4.4). The publisher OWNS NO TIMER — it rides the heartbeat at a
/// downsampled cadence (`PublishEveryNTicks`).
///
/// GOVERNING RULE (§4.4): a publisher error must never abort or delay a heartbeat tick. Every path
/// here swallows-and-logs; the only thing that propagates is cooperative cancellation.
/// </summary>
public sealed class SocialHeartbeatHook
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SocialPublisherOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<SocialHeartbeatHook> _logger;
    private int _tick;

    public SocialHeartbeatHook(
        IServiceScopeFactory scopeFactory,
        SocialPublisherOptions options,
        IClock clock,
        ILogger<SocialHeartbeatHook> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Called once per heartbeat tick. Runs the publisher every Nth tick, never throwing.</summary>
    public async Task OnHeartbeatTickAsync(CancellationToken ct)
    {
        var n = Interlocked.Increment(ref _tick);
        if (_options.PublishEveryNTicks <= 0 || n % _options.PublishEveryNTicks != 0)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<ISocialPublisher>();
            await RunSafelyAsync(publisher, _clock.Now, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Scope/resolution failure must not abort the heartbeat.
            _logger.LogError(ex, "SocialPublisher hook failed (swallowed; core platform unaffected).");
        }
    }

    /// <summary>
    /// Invokes the publisher and swallows any escaped exception (§4.4). Returns true on a clean run,
    /// false if the publisher threw — but NEVER propagates. Testable in isolation (Gate 6.1).
    /// </summary>
    public async Task<bool> RunSafelyAsync(ISocialPublisher publisher, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            await publisher.RunOnceAsync(now, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SocialPublisher.RunOnceAsync threw; swallowed so the heartbeat continues.");
            return false;
        }
    }
}
