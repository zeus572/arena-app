using Arena.API.Models;

namespace Arena.API.Services;

public class LlmTurnResult
{
    public string Content { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
}

public class Citation
{
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class CommentaryResult
{
    public string CommentatorAContent { get; set; } = string.Empty;
    public string CommentatorBContent { get; set; } = string.Empty;
}

public interface ILlmService
{
    Task<LlmTurnResult> GenerateTurnAsync(Agent agent, Debate debate, List<Turn> previousTurns, TurnType turnType = TurnType.Argument, string? crowdQuestion = null);
    Task<CommentaryResult> GenerateCommentaryAsync(Agent commentatorA, Agent commentatorB, Debate debate, List<Turn> previousTurns);
}
