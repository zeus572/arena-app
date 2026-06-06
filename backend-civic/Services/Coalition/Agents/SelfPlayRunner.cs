using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>Telemetry from a self-play run.</summary>
public sealed record SelfPlayResult(
    CoalitionOutcome? Outcome,
    IReadOnlyList<double> DistanceHistory,
    int Rounds,
    bool Stalled);

/// <summary>
/// Phase 2.4 — the agent self-play harness (also the reusable regression engine,
/// Part E). It drives the loop autonomously: each round every agent picks an act
/// (pure <see cref="AgentActPolicy"/>) and the <see cref="ProvisionStateMachine"/>
/// applies it, until the provision resolves, a full round produces no act (stall),
/// or a round cap is hit. The distance-to-coalition signal is recorded each round.
///
/// Entirely pure (no LLM): agents are constructed/Values-projected; their per-act
/// cognition is geometry. This is the de-risk milestone — it demonstrates the core
/// thesis end-to-end without any model call.
/// </summary>
public sealed class SelfPlayRunner
{
    private readonly ProvisionStateMachine _sm;

    public SelfPlayRunner(ProvisionStateMachine? stateMachine = null)
        => _sm = stateMachine ?? new ProvisionStateMachine();

    public SelfPlayResult Run(ProvisionLoopState state, IReadOnlyList<CoalitionAgent> agents, int maxRounds = 50)
    {
        var distances = new List<double> { CurrentDistance(state) };
        var rounds = 0;
        var stalled = false;

        while (!state.IsTerminal && rounds < maxRounds)
        {
            rounds++;
            var actedThisRound = false;

            foreach (var agent in agents)
            {
                if (state.IsTerminal) break;
                var act = AgentActPolicy.ChooseAct(agent, state);
                if (act is null) continue;
                _sm.Apply(state, act);
                actedThisRound = true;
            }

            distances.Add(CurrentDistance(state));

            if (!actedThisRound)
            {
                stalled = true;
                break;
            }
        }

        return new SelfPlayResult(state.Outcome, distances, rounds, stalled);
    }

    /// <summary>Normalized distance-to-coalition over the current toothful versions (telemetry only).</summary>
    private static double CurrentDistance(ProvisionLoopState state)
    {
        var candidates = state.Versions.Where(v => v.Specificity >= state.Config.MinTeethSpecificity).ToList();
        return DistanceCalculator.DistanceToCoalition(candidates, state.RequiredPlayers).Normalized;
    }
}
