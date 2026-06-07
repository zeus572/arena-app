using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Human;
using Civic.API.Services.Coalition.Loop;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>
/// Phase 2H.2 — runs one provision with agents AND humans co-participating (agents
/// as seed/ballast in thin early leagues, A6/Part C). Each round, one queued human
/// act is applied, then every agent picks an act; both go through the SAME state
/// machine. Pure — no LLM.
/// </summary>
public sealed class MixedPlayRunner
{
    private readonly ProvisionStateMachine _sm = new();

    public SelfPlayResult Run(
        ProvisionLoopState state,
        IReadOnlyList<CoalitionAgent> agents,
        IEnumerable<HumanAct> humanScript,
        int maxRounds = 50)
    {
        var humans = new Queue<HumanAct>(humanScript);
        var distances = new List<double> { Distance(state) };
        var rounds = 0;
        var stalled = false;

        while (!state.IsTerminal && rounds < maxRounds)
        {
            rounds++;
            var acted = false;

            if (humans.Count > 0)
            {
                var human = humans.Dequeue();
                var loopAct = HumanActTranslator.ToLoopAct(human);
                if (loopAct is not null) _sm.Apply(state, loopAct);
                acted = true; // engagement-only acts still count as participation this round
            }

            foreach (var agent in agents)
            {
                if (state.IsTerminal) break;
                var act = AgentActPolicy.ChooseAct(agent, state);
                if (act is null) continue;
                _sm.Apply(state, act);
                acted = true;
            }

            distances.Add(Distance(state));
            if (!acted) { stalled = true; break; }
        }

        if (!state.IsTerminal)
        {
            _sm.Apply(state, new AdvanceToDeadlineAct());
            distances.Add(Distance(state));
        }

        return new SelfPlayResult(state.Outcome, distances, rounds, stalled);
    }

    private static double Distance(ProvisionLoopState state)
    {
        var candidates = state.Versions.Where(v => v.Specificity >= state.Config.MinTeethSpecificity).ToList();
        return DistanceCalculator.DistanceToCoalition(candidates, state.RequiredPlayers).Normalized;
    }
}
