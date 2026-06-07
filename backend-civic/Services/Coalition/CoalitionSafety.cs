using System.Reflection;
using Civic.API.Services.Coalition.Human;
using Civic.API.Services.Coalition.Loop;

namespace Civic.API.Services.Coalition;

/// <summary>
/// Safety invariants for the coalition surface (principle A8). The whole surface is
/// BROADCAST-ONLY: an act references its actor and public artifacts (provision /
/// version), but NEVER a second user — there is no private channel between two
/// users, ever, including as a reward. This is enforced structurally: a coalition
/// act type may not carry a recipient/target-user field. The check is reflective so
/// it keeps biting if someone later adds a private-channel act.
/// </summary>
public static class CoalitionSafety
{
    // Property names that would indicate a private, user-to-user channel.
    private static readonly HashSet<string> RecipientLike = new(StringComparer.OrdinalIgnoreCase)
    {
        "recipient", "recipientid", "recipientuserid",
        "target", "targetuser", "targetuserid",
        "touser", "touserid", "dm", "dmuserid", "peer", "peeruserid",
    };

    /// <summary>All concrete coalition act types (loop acts + human acts).</summary>
    public static IReadOnlyList<Type> CoalitionActTypes()
    {
        var asm = typeof(LoopAct).Assembly;
        return asm.GetTypes()
            .Where(t => !t.IsAbstract &&
                        (typeof(LoopAct).IsAssignableFrom(t) || typeof(HumanAct).IsAssignableFrom(t)))
            .ToList();
    }

    /// <summary>An act type is broadcast-only iff it exposes no recipient/target-user property.</summary>
    public static bool IsBroadcastOnly(Type actType) =>
        !actType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => RecipientLike.Contains(p.Name));

    /// <summary>True iff EVERY coalition act type is broadcast-only.</summary>
    public static bool AllActsBroadcastOnly(out IReadOnlyList<string> violations)
    {
        var bad = CoalitionActTypes().Where(t => !IsBroadcastOnly(t)).Select(t => t.Name).ToList();
        violations = bad;
        return bad.Count == 0;
    }
}
