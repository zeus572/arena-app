using Civic.API.Data;

namespace Civic.API.Services;

/// <summary>Uniform (user, timestamp) projection so every feature can share one aggregator.</summary>
public sealed class UserEvent
{
    public string UserId { get; set; } = "";
    public DateTime At { get; set; }
}

/// <summary>
/// One tracked user action: its stable key, display label, area grouping, and a factory
/// for the deferred query that yields its (user, timestamp) rows.
/// </summary>
/// <param name="Build">Deferred — call it per consumer so each can add its own filters.
/// The queries close over a single <see cref="CivicDbContext"/>, so consumers MUST execute
/// them sequentially (a DbContext is not thread-safe).</param>
public sealed record EngagementFeature(
    string Key,
    string Label,
    string Area,
    Func<IQueryable<UserEvent>> Build);

/// <summary>
/// The single definition of "what counts as engagement" in Civic — which tables map to
/// which user-facing action, and how each one yields (user, timestamp). Both the admin
/// engagement dashboard (cumulative + 7/30d windows) and the daily-stats endpoint (one
/// UTC day) read from this list, so a new feature is tracked in both the moment it's
/// added here.
/// </summary>
public static class EngagementCatalog
{
    // Area labels (also the display grouping / order).
    public const string Onboarding = "Onboarding";
    public const string Exercises = "Exercises";
    public const string Coalitions = "Coalitions";
    public const string Candidates = "AI candidates";
    public const string Social = "Shorts & posts";
    public const string Groups = "Leagues & circles";
    public const string Petitions = "Petitions";

    public static readonly string[] AreaOrder =
        { Onboarding, Exercises, Coalitions, Candidates, Social, Groups, Petitions };

    /// <summary>The anonymous sentinel user id. Rows carrying it are real volume but can't
    /// be attributed to an account, so consumers count them separately.</summary>
    public const string AnonymousUserId = "anonymous";

    /// <summary>
    /// Projections filter agents and null owners at the source, so consumers only have to
    /// decide about anonymous rows and time windows.
    /// </summary>
    public static IReadOnlyList<EngagementFeature> For(CivicDbContext db) => new EngagementFeature[]
    {
        new("profile", "Profile / compass built", Onboarding,
            () => db.UserProfiles.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("compass_answer", "Compass questions answered", Onboarding,
            () => db.CivicAnswers.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("quiz", "Knowledge quiz answered", Onboarding,
            () => db.QuizResponses.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

        new("budget", "Budget exercise run", Exercises,
            () => db.BudgetSessions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("values_receipt", "Values receipt generated", Exercises,
            () => db.ValuesReceipts.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

        new("coalition_position", "Coalition stance taken", Coalitions,
            () => db.ProvisionPositions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("coalition_accept", "Provision co-signed", Coalitions,
            () => db.AcceptanceRecords.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("coalition_amend", "Amendment proposed", Coalitions,
            () => db.Amendments.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("coalition_join", "Coalition loop joined", Coalitions,
            () => db.CoalitionParticipants.Where(x => !x.IsAgent).Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("reasoning_act", "Reasoning-XP act (any)", Coalitions,
            () => db.CoalitionActs.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

        new("candidate_follow", "AI candidate followed", Candidates,
            () => db.CandidateFollows.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("candidate_mute", "AI candidate muted", Candidates,
            () => db.CandidateMutes.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("campaign_run", "Campaign Manager run", Candidates,
            () => db.CivicCampaigns.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

        new("post_authored", "Campaign/short post authored", Social,
            () => db.CampaignPosts.Where(x => x.OwnerUserId != null).Select(x => new UserEvent { UserId = x.OwnerUserId!, At = x.CreatedAt })),
        new("post_reaction", "Post/short reacted to", Social,
            () => db.PostReactions.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),

        new("league_owned", "League created", Groups,
            () => db.Leagues.Select(x => new UserEvent { UserId = x.OwnerUserId, At = x.CreatedAt })),
        new("league_member", "League joined", Groups,
            () => db.LeagueMembers.Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),
        new("league_entry", "League round entry", Groups,
            () => db.LeagueRoundEntries.Select(x => new UserEvent { UserId = x.UserId, At = x.CreatedAt })),
        new("cohort_member", "Cohort placement", Groups,
            () => db.CohortMembers.Where(x => !x.IsAgent).Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),
        new("circle_member", "Circle placement", Groups,
            () => db.CoalitionCircleMembers.Select(x => new UserEvent { UserId = x.UserId, At = x.JoinedAt })),

        new("petition_created", "Petition created", Petitions,
            () => db.Petitions.Select(x => new UserEvent { UserId = x.CreatedBy, At = x.CreatedAt })),
    };
}
