namespace Arena.API.Models;

public class DebateFormatConfig
{
    public string Format { get; set; } = "standard";
    public string DisplayName { get; set; } = "Standard Debate";
    public string Description { get; set; } = "";
    public int MaxTurns { get; set; } = 8;
    public int MaxTokens { get; set; } = 1024;
    public int? MaxCharactersPerTurn { get; set; }
    public bool HasCompromisePhase { get; set; }
    public bool HasWildcards { get; set; }
    public double WildcardChance { get; set; }
    public int WildcardStartTurn { get; set; } = 4;
    public bool HasCommentary { get; set; } = true;
    public int CommentaryEveryNTurns { get; set; } = 2;
    public bool HasTools { get; set; } = true;
    public int MaxToolRounds { get; set; } = 5;
    public bool HasBudgetTable { get; set; }
    public int TurnDelaySeconds { get; set; } = 30;
    public double EngagementMultiplier { get; set; } = 1.0;
    public double RecencyHalfLifeHours { get; set; } = 48;

    public static readonly Dictionary<string, DebateFormatConfig> All = new()
    {
        ["standard"] = new()
        {
            Format = "standard", DisplayName = "Standard Debate",
            Description = "Classic 6-turn debate with compromise phase and budget proposals",
            MaxTurns = 8, MaxTokens = 1024,
            HasCompromisePhase = true, HasWildcards = true, WildcardChance = 0.20,
            HasCommentary = true, CommentaryEveryNTurns = 2,
            HasTools = true, MaxToolRounds = 5, HasBudgetTable = true,
            EngagementMultiplier = 1.0, RecencyHalfLifeHours = 48,
        },
        ["common_ground"] = new()
        {
            Format = "common_ground", DisplayName = "Common Ground",
            Description = "Opponents find genuine agreement with citations — no fighting, just surprising overlap",
            MaxTurns = 4, MaxTokens = 1024,
            HasCompromisePhase = false, HasWildcards = false,
            HasCommentary = true, CommentaryEveryNTurns = 2,
            HasTools = true, MaxToolRounds = 5, HasBudgetTable = false,
            EngagementMultiplier = 1.5, RecencyHalfLifeHours = 48,
        },
        ["tweet"] = new()
        {
            Format = "tweet", DisplayName = "Hot Take Battle",
            Description = "280-character limit, 10 rounds of short-form political hot takes",
            MaxTurns = 10, MaxTokens = 128, MaxCharactersPerTurn = 280,
            HasCompromisePhase = false, HasWildcards = true, WildcardChance = 0.30,
            HasCommentary = true, CommentaryEveryNTurns = 2,
            HasTools = true, MaxToolRounds = 1, HasBudgetTable = false,
            TurnDelaySeconds = 10,
            EngagementMultiplier = 2.0, RecencyHalfLifeHours = 24,
        },
        ["rapid_fire"] = new()
        {
            Format = "rapid_fire", DisplayName = "Rapid Fire",
            Description = "1-2 sentences per turn, 14 rounds of verbal sparring — no tools, pure rhetoric",
            MaxTurns = 14, MaxTokens = 200, MaxCharactersPerTurn = 500,
            HasCompromisePhase = false, HasWildcards = false,
            HasCommentary = true, CommentaryEveryNTurns = 4,
            HasTools = false, MaxToolRounds = 0, HasBudgetTable = false,
            EngagementMultiplier = 1.0, RecencyHalfLifeHours = 24,
        },
        ["longform"] = new()
        {
            Format = "longform", DisplayName = "Longform Essay",
            Description = "500-800 word essays, 4 turns total — deep, sourced, substantive arguments",
            MaxTurns = 4, MaxTokens = 4096,
            HasCompromisePhase = false, HasWildcards = false,
            HasCommentary = false,
            HasTools = true, MaxToolRounds = 8, HasBudgetTable = false,
            EngagementMultiplier = 0.8, RecencyHalfLifeHours = 72,
        },
        ["roast"] = new()
        {
            Format = "roast", DisplayName = "Roast Battle",
            Description = "Stand-up comedy meets policy debate — destroy your opponent with humor",
            MaxTurns = 8, MaxTokens = 512,
            HasCompromisePhase = false, HasWildcards = true, WildcardChance = 0.40, WildcardStartTurn = 2,
            HasCommentary = true, CommentaryEveryNTurns = 2,
            HasTools = true, MaxToolRounds = 5, HasBudgetTable = false,
            EngagementMultiplier = 1.5, RecencyHalfLifeHours = 48,
        },
        ["town_hall"] = new()
        {
            Format = "town_hall", DisplayName = "Town Hall",
            Description = "One agent on the hot seat, multiple questioners grilling them — 5 Q&A pairs",
            MaxTurns = 10, MaxTokens = 1024,
            HasCompromisePhase = false, HasWildcards = false,
            HasCommentary = true, CommentaryEveryNTurns = 2,
            HasTools = true, MaxToolRounds = 5, HasBudgetTable = false,
            EngagementMultiplier = 1.2, RecencyHalfLifeHours = 48,
        },
    };

    public static DebateFormatConfig Get(string format)
        => All.TryGetValue(format, out var config) ? config : All["standard"];
}
