using Arena.API.Models;

namespace Arena.API.Services;

public class TopicGeneratorService
{
    private static readonly string[] Topics =
    {
        "Should AI systems be heavily regulated by governments?",
        "Is universal basic income a path to freedom or dependency?",
        "Can economic growth coexist with environmental sustainability?",
        "Should social media companies be held liable for user content?",
        "Is democracy the best form of government for the 21st century?",
        "Should education be fully publicly funded through university?",
        "Is privacy a fundamental right, even at the cost of security?",
        "Should voting be mandatory in democracies?",
        "Is capitalism inherently exploitative or naturally empowering?",
        "Should nation-states have open borders?",
        "Is nuclear energy the solution to the climate crisis?",
        "Should we pursue space colonization or fix Earth first?",
        "Is intellectual property helping or hindering innovation?",
        "Should healthcare be treated as a public good or a market?",
        "Is cancel culture a threat to free speech or accountability in action?",
        "Should we tax wealth rather than income?",
        "Is globalization a net positive for developing nations?",
        "Should genetic engineering of humans be permitted?",
        "Is meritocracy a myth in modern societies?",
        "Should criminal justice focus on rehabilitation over punishment?",
        "Is digital surveillance justified in the name of national security?",
        "Should the gig economy be more regulated?",
        "Is cultural appropriation a valid concern or an overreaction?",
        "Should autonomous weapons be banned by international law?",
        "Is degrowth a viable economic strategy?",
    };

    private readonly Random _rng = new();

    public string PickRandomTopic()
    {
        return Topics[_rng.Next(Topics.Length)];
    }

    public (Guid proponentId, Guid opponentId) PickAgentPair(List<Agent> agents)
    {
        if (agents.Count < 2)
            throw new InvalidOperationException("Need at least 2 agents.");

        var shuffled = agents.OrderBy(_ => _rng.Next()).ToList();
        return (shuffled[0].Id, shuffled[1].Id);
    }
}
