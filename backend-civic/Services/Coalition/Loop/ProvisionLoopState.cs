using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Loop;

/// <summary>
/// In-memory snapshot of one provision's live loop: the engaged players (each a
/// region in sub-question space + a spectrum bucket), the candidate versions, the
/// acceptance history, the deadline, and the current state. Mutated only through
/// <see cref="ProvisionStateMachine"/>. Persisting this to EF (Provision.State,
/// versions, acceptance records) is a thin adapter deferred out of Layer 2's
/// computational core — recorded assumption.
/// </summary>
public sealed class ProvisionLoopState
{
    public string ProvisionId { get; }
    public ComposedSpectrum Spectrum { get; }
    public LoopConfig Config { get; }

    public ProvisionState State { get; set; } = ProvisionState.Open;
    public DateTime? Deadline { get; set; }
    public DateTime Now { get; set; }
    public CoalitionOutcome? Outcome { get; set; }

    // Engaged roster (the required, spectrum-spanning coalition we are trying to form).
    private readonly Dictionary<string, PlayerGeometry> _players = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Positioned { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<VersionPoint> Versions { get; } = new();
    public List<LoopAcceptance> Acceptances { get; } = new();

    public ProvisionLoopState(
        string provisionId,
        IEnumerable<PlayerGeometry> roster,
        ComposedSpectrum spectrum,
        LoopConfig? config = null,
        DateTime? start = null,
        TimeSpan? lifetime = null,
        IEnumerable<VersionPoint>? initialVersions = null)
    {
        ProvisionId = provisionId;
        Spectrum = spectrum;
        Config = config ?? new LoopConfig();
        foreach (var p in roster) _players[p.UserId] = p;
        Now = start ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Deadline = lifetime is null ? null : Now + lifetime.Value;
        if (initialVersions is not null) Versions.AddRange(initialVersions);
    }

    public IReadOnlyCollection<PlayerGeometry> Players => _players.Values;

    /// <summary>The required coalition = the full engaged roster (the spectrum-spanning set).</summary>
    public IReadOnlyList<PlayerGeometry> RequiredPlayers => _players.Values.ToList();

    public PlayerGeometry? PlayerOrNull(string userId) =>
        _players.TryGetValue(userId, out var p) ? p : null;

    public bool IsTerminal =>
        State is ProvisionState.Passed or ProvisionState.Forked or ProvisionState.Died;

    /// <summary>Per-user acceptance signals (for movement detection), in chronological order.</summary>
    public IReadOnlyList<AcceptanceSignal> SignalsFor(string userId) =>
        Acceptances.Where(a => string.Equals(a.UserId, userId, StringComparison.OrdinalIgnoreCase))
                   .OrderBy(a => a.At)
                   .Select(a => new AcceptanceSignal(a.Version, a.Accept, a.At))
                   .ToList();

    /// <summary>Latest accept/decline a user gave for a given version configuration (null if none).</summary>
    public bool? LatestAcceptance(string userId, VersionPoint version)
    {
        var canon = version.Canonical();
        var last = Acceptances
            .Where(a => string.Equals(a.UserId, userId, StringComparison.OrdinalIgnoreCase)
                        && a.Version.Canonical() == canon)
            .OrderBy(a => a.At)
            .LastOrDefault();
        return last?.Accept;
    }
}
