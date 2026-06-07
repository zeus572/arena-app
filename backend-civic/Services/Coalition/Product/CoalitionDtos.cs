namespace Civic.API.Services.Coalition.Product;

// Read models + request DTOs for the coalition product API.

public sealed record SpectrumCellDto(string Bucket, bool Covered);

public sealed record SpectrumBarDto(
    IReadOnlyList<SpectrumCellDto> Cells,
    int CoveredBuckets,
    int TotalBuckets,
    double Distance,
    DateTime? Deadline,
    Guid? LeadingVersionId);

public sealed record SubQuestionDto(string Key, string Prompt, string? Tradeoff, string[] Options, string Origin);

public sealed record VersionDto(
    Guid Id,
    string? Label,
    string Text,
    Dictionary<string, string> Positions,
    int Specificity,
    string? AuthorUserId,
    int Accepts,
    int Declines);

public sealed record ParticipantDto(string UserId, string Bucket, bool IsAgent, bool HasPositioned);

public sealed record OutcomeDto(
    string FinalState,
    Guid? PlankVersionId,
    string[]? Signers,
    int CoveredBuckets,
    int Specificity,
    int MovedSigners,
    string? DiedReason);

public sealed record ProvisionSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string State,
    double Distance,
    int CoveredBuckets,
    int TotalBuckets,
    DateTime? Deadline,
    double GapWidth,
    string Difficulty,
    bool Governance);

public sealed record ProvisionDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string NeutralText,
    string State,
    string[] RelevantAxes,
    DateTime? Deadline,
    IReadOnlyList<SubQuestionDto> SubQuestions,
    IReadOnlyList<VersionDto> Versions,
    IReadOnlyList<ParticipantDto> Participants,
    SpectrumBarDto SpectrumBar,
    OutcomeDto? Outcome,
    string? YourUserId,
    bool YouJoined,
    double GapWidth,
    string Difficulty,
    bool Governance);

// ---- Layer 3 gamification read models ----

public sealed record CampaignRecordDto(
    int PlanksPassed, int TotalBreadth, double AvgBreadth, int TotalMovedSigners,
    double GovernanceRatio, double WeightedScore);

public sealed record CadenceDto(double Score, bool[] Last7Days);

public sealed record PlankDto(Guid ProvisionId, string Title, int Breadth, double GapWidth, bool Governance);

public sealed record RecommendedProvisionDto(Guid Id, string Title, string State, double GapWidth, string Difficulty);

public sealed record MeDto(
    string UserId,
    double Skill,
    string SkillLabel,
    CampaignRecordDto Record,
    CadenceDto Cadence,
    string? LeagueId,
    string? LeagueName,
    double LeagueGapTier,
    string Movement,
    IReadOnlyList<PlankDto> RecentPlanks,
    IReadOnlyList<RecommendedProvisionDto> Recommended);

public sealed record StandingRowDto(
    int Rank, string UserId, string DisplayName, bool IsAgent, double Score,
    int CoalitionsSigned, int TotalBreadth, int MovedCount);

public sealed record LeagueDto(
    string Id, string Name, double GapTier, string DifficultyLabel,
    IReadOnlyList<string> Buckets, IReadOnlyList<StandingRowDto> Standings);

public sealed record JoinRequest(string? Bucket);
public sealed record PositionRequest(string Stance, string Intensity = "Medium", string? Bucket = null, string? ReasoningTag = null);
public sealed record AmendmentRequest(Dictionary<string, string> Positions, string? Label = null);
public sealed record AcceptanceRequest(Guid VersionId, bool Accept, string Intensity = "Medium");
