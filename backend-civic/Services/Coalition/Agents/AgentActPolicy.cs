using Civic.API.Services.Coalition.Geometry;
using Civic.API.Services.Coalition.Loop;

namespace Civic.API.Services.Coalition.Agents;

/// <summary>
/// Phase 2.2 — the agent act policy (Part C). Given the provision state, the agent
/// chooses among {take position, propose amendment, accept/decline}. Pure: every
/// choice is computed from the agent's region + the geometry, no LLM.
///
/// Amendment proposal = "what carve-out would move this version into my acceptance
/// set without violating a high-intensity position" — implemented as flipping the
/// version's conflicting sub-questions to the agent's own acceptable labels (which,
/// by construction, never violates the agent's own positions).
/// </summary>
public static class AgentActPolicy
{
    public static LoopAct? ChooseAct(CoalitionAgent agent, ProvisionLoopState state)
    {
        if (state.IsTerminal) return null;

        // 1. Declare engagement first.
        if (!state.Positioned.Contains(agent.UserId))
            return new TakePositionAct(agent.UserId, DeriveStance(agent), agent.DefaultIntensity);

        var toothful = state.Versions.Where(v => v.Specificity >= 1).ToList();

        // 2. Co-sign the BROADEST version we accept but haven't signed (drives convergence
        //    toward a shared bridge rather than each agent's pet version).
        var acceptable = toothful
            .Where(v => AgentAcceptancePolicy.WouldSign(agent, v).Accept)
            .Where(v => state.LatestAcceptance(agent.UserId, v) != true)
            .OrderByDescending(v => OverlapCalculator.SupportCount(state.Players, v))
            .ThenByDescending(v => v.Specificity)
            .ToList();
        if (acceptable.Count > 0)
        {
            var v = acceptable[0];
            return new CastAcceptanceAct(agent.UserId, v, true, AgentAcceptancePolicy.WouldSign(agent, v).Intensity);
        }

        // 3. Otherwise, take the leading version we reject and propose a carve-out that
        //    pulls it into our acceptance set.
        var rejected = toothful
            .Where(v => !AgentAcceptancePolicy.WouldSign(agent, v).Accept)
            .OrderByDescending(v => OverlapCalculator.SupportCount(state.Players, v))
            .ToList();
        foreach (var v in rejected)
        {
            var bridge = ProposeBridge(agent, v);
            if (bridge is not null && state.Versions.All(x => x.Canonical() != bridge.Canonical()))
                return new ProposeAmendmentAct(agent.UserId, bridge);
        }

        // 4. Nothing constructive left: record a principled decline of the leading
        //    rejected version (once), so the disagreement is logged honestly.
        foreach (var v in rejected)
        {
            if (state.LatestAcceptance(agent.UserId, v) != false)
                return new CastAcceptanceAct(agent.UserId, v, false, AgentAcceptancePolicy.WouldSign(agent, v).Intensity);
        }

        return null; // no useful act this round
    }

    /// <summary>
    /// Build a carve-out of <paramref name="baseVersion"/> that the agent would accept, by
    /// flipping each conflicting sub-question to one of the agent's acceptable labels. Returns
    /// null if some conflict can't be satisfied. Never violates the agent's own positions.
    /// </summary>
    public static VersionPoint? ProposeBridge(CoalitionAgent agent, VersionPoint baseVersion)
    {
        var positions = new Dictionary<string, string>(baseVersion.Positions, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, label) in baseVersion.Positions)
        {
            if (agent.Region.ConstrainedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                && !agent.Region.AcceptableLabels(key).Contains(label))
            {
                var fix = agent.Region.AcceptableLabels(key).FirstOrDefault();
                if (fix is null) return null; // cannot satisfy this conflict
                positions[key] = fix;
            }
        }

        var bridge = new VersionPoint($"{baseVersion.Id}+{agent.UserId}", positions);
        return AgentAcceptancePolicy.WouldSign(agent, bridge).Accept ? bridge : null;
    }

    private static string DeriveStance(CoalitionAgent agent)
    {
        if (agent.Region.IsUnconstrained) return "open to most configurations";
        var parts = agent.Region.ConstrainedKeys
            .Select(k => $"{k}={string.Join("/", agent.Region.AcceptableLabels(k))}");
        return "prefers " + string.Join("; ", parts);
    }
}
