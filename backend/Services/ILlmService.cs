using Arena.API.Models;

namespace Arena.API.Services;

public interface ILlmService
{
    Task<string> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns);
}
