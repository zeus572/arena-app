using Civic.API.Models;
using Civic.API.Services.Coalition.Geometry;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>
/// A coalition agent for self-play (Part C). It is Values-grounded via its
/// acceptance REGION in sub-question space (what positions it would co-sign) plus
/// a per-sub-question INTENSITY (how hard it holds each), and it occupies a
/// segment of the Values spectrum (<see cref="SpectrumBucket"/>).
///
/// The region+intensities are the structured projection of the agent's Values
/// profile/sources onto a provision. Producing them from a real celebrity/historical
/// profile is the one LLM step (the <c>IAgentProfileMapper</c> seam, deferred /
/// stubbed); once produced, all per-version reasoning (<see cref="AgentAcceptancePolicy"/>)
/// is pure geometry. Tests construct agents directly.
/// </summary>
public sealed class CoalitionAgent
{
    public string UserId { get; }
    public string SpectrumBucket { get; }
    public AcceptanceRegion Region { get; }
    public AnswerIntensity DefaultIntensity { get; }
    private readonly Dictionary<string, AnswerIntensity> _intensities;

    public CoalitionAgent(
        string userId,
        string spectrumBucket,
        AcceptanceRegion region,
        IReadOnlyDictionary<string, AnswerIntensity>? intensities = null,
        AnswerIntensity defaultIntensity = AnswerIntensity.Medium)
    {
        UserId = userId;
        SpectrumBucket = spectrumBucket;
        Region = region;
        DefaultIntensity = defaultIntensity;
        _intensities = intensities is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(intensities, StringComparer.OrdinalIgnoreCase);
    }

    public AnswerIntensity IntensityFor(string key) =>
        _intensities.TryGetValue(key, out var i) ? i : DefaultIntensity;

    public bool HasHighIntensityOn(string key) =>
        IntensityFor(key) is AnswerIntensity.High or AnswerIntensity.NonNegotiable;

    public bool HasNonNegotiableOn(string key) =>
        IntensityFor(key) == AnswerIntensity.NonNegotiable;

    /// <summary>Project to the geometry player type used by the loop and Layer 1.</summary>
    public PlayerGeometry ToPlayer() => new(UserId, Region, SpectrumBucket);
}
