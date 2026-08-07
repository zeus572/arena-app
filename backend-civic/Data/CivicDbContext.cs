using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Civic.API.Models;
using Civic.API.Models.Daily;
using Civic.API.Models.Rooms;
using Arena.Shared.Social;

namespace Civic.API.Data;

public class CivicDbContext : DbContext
{
    public CivicDbContext(DbContextOptions<CivicDbContext> options) : base(options) { }

    public DbSet<Petition> Petitions => Set<Petition>();
    public DbSet<Briefing> Briefings => Set<Briefing>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<ThinkDeeper> ThinkDeepers => Set<ThinkDeeper>();
    public DbSet<CivicQuestion> CivicQuestions => Set<CivicQuestion>();
    public DbSet<CivicAnswer> CivicAnswers => Set<CivicAnswer>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ProfileAxisScore> ProfileAxisScores => Set<ProfileAxisScore>();
    public DbSet<BudgetSession> BudgetSessions => Set<BudgetSession>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();
    public DbSet<ValuesReceipt> ValuesReceipts => Set<ValuesReceipt>();
    public DbSet<Election> Elections => Set<Election>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizResponse> QuizResponses => Set<QuizResponse>();
    public DbSet<Cohort> Cohorts => Set<Cohort>();
    public DbSet<CohortMember> CohortMembers => Set<CohortMember>();
    public DbSet<BillTimelineStep> BillTimelineSteps => Set<BillTimelineStep>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillAxisPosition> BillAxisPositions => Set<BillAxisPosition>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<VirtualCandidate> VirtualCandidates => Set<VirtualCandidate>();
    public DbSet<CandidateAxisScore> CandidateAxisScores => Set<CandidateAxisScore>();
    public DbSet<CandidateIssueTone> CandidateIssueTones => Set<CandidateIssueTone>();
    public DbSet<PlatformPlank> PlatformPlanks => Set<PlatformPlank>();
    public DbSet<CandidateSource> CandidateSources => Set<CandidateSource>();
    public DbSet<CampaignPost> CampaignPosts => Set<CampaignPost>();
    public DbSet<PostFragment> PostFragments => Set<PostFragment>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<ElectionCycle> ElectionCycles => Set<ElectionCycle>();
    public DbSet<CandidateFollow> CandidateFollows => Set<CandidateFollow>();
    public DbSet<CandidateMute> CandidateMutes => Set<CandidateMute>();

    // Campaign Manager game mode.
    public DbSet<CivicCampaign> CivicCampaigns => Set<CivicCampaign>();
    public DbSet<CivicCampaignStanding> CivicCampaignStandings => Set<CivicCampaignStanding>();
    public DbSet<CivicCampaignWeek> CivicCampaignWeeks => Set<CivicCampaignWeek>();
    public DbSet<CivicCampaignAction> CivicCampaignActions => Set<CivicCampaignAction>();
    public DbSet<CandidateNewsResponse> CandidateNewsResponses => Set<CandidateNewsResponse>();

    // Leagues: social competition groups.
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMember> LeagueMembers => Set<LeagueMember>();
    public DbSet<LeagueInvite> LeagueInvites => Set<LeagueInvite>();
    public DbSet<LeagueRound> LeagueRounds => Set<LeagueRound>();
    public DbSet<LeagueRoundEntry> LeagueRoundEntries => Set<LeagueRoundEntry>();

    // Coalition game (Layer 0): provisions & structured engagement.
    public DbSet<Provision> Provisions => Set<Provision>();
    public DbSet<SubQuestion> SubQuestions => Set<SubQuestion>();
    public DbSet<ProvisionPosition> ProvisionPositions => Set<ProvisionPosition>();
    public DbSet<Amendment> Amendments => Set<Amendment>();
    public DbSet<ProvisionVersion> ProvisionVersions => Set<ProvisionVersion>();
    public DbSet<AcceptanceRecord> AcceptanceRecords => Set<AcceptanceRecord>();
    public DbSet<ExtractionCacheEntry> ExtractionCacheEntries => Set<ExtractionCacheEntry>();
    public DbSet<CoalitionParticipant> CoalitionParticipants => Set<CoalitionParticipant>();

    // SocialPublisher (shared engine) — the only table the civic publisher writes.
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
    public DbSet<CoalitionCircle> CoalitionCircles => Set<CoalitionCircle>();
    public DbSet<CoalitionCircleMember> CoalitionCircleMembers => Set<CoalitionCircleMember>();
    public DbSet<CoalitionActivityDay> CoalitionActivityDays => Set<CoalitionActivityDay>();
    public DbSet<CoalitionAct> CoalitionActs => Set<CoalitionAct>();

    // Casual daily games (docs/civic_daily_games). One generic pair of tables serves every
    // kind — per-game shape lives in DailyPuzzle.PayloadJson, so another game is an enum
    // member and a payload contract, not a migration. (Which Is True, the seventh, shipped
    // without one.)
    public DbSet<DailyPuzzle> DailyPuzzles => Set<DailyPuzzle>();
    public DbSet<DailyPuzzlePlay> DailyPuzzlePlays => Set<DailyPuzzlePlay>();

    // Topic Rooms knowledge graph (docs/Rooms Expansion).
    public DbSet<ObjectLink> ObjectLinks => Set<ObjectLink>();
    public DbSet<SourceRef> SourceRefs => Set<SourceRef>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimStatusHistory> ClaimStatusHistories => Set<ClaimStatusHistory>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ThemeRoom> ThemeRooms => Set<ThemeRoom>();
    public DbSet<StoryRoom> StoryRooms => Set<StoryRoom>();
    public DbSet<RoomRevision> RoomRevisions => Set<RoomRevision>();
    public DbSet<ChangeLogEntry> ChangeLogEntries => Set<ChangeLogEntry>();
    public DbSet<UserRoomState> UserRoomStates => Set<UserRoomState>();
    public DbSet<Actor> Actors => Set<Actor>();
    public DbSet<ActorRoomRole> ActorRoomRoles => Set<ActorRoomRole>();
    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();
    public DbSet<Development> Developments => Set<Development>();
    public DbSet<ReviewFlag> ReviewFlags => Set<ReviewFlag>();
    public DbSet<PublishGateResult> PublishGateResults => Set<PublishGateResult>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<RoomInteractionPlay> RoomInteractionPlays => Set<RoomInteractionPlay>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<UserPrediction> UserPredictions => Set<UserPrediction>();
    public DbSet<MoneyItem> MoneyItems => Set<MoneyItem>();
    public DbSet<MoneyStageEntry> MoneyStageEntries => Set<MoneyStageEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Petition>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.CreatedAt).IsDescending();
        });

        modelBuilder.Entity<Briefing>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.Slug).IsUnique();
            e.HasIndex(b => b.IssueOrder);
            e.OwnsMany(b => b.WordsToKnow, ow =>
            {
                ow.WithOwner().HasForeignKey("BriefingId");
                ow.Property<int>("Id");
                ow.HasKey("Id");
                ow.ToTable("BriefingWordsToKnow");
            });
        });

        modelBuilder.Entity<Concept>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
        });

        modelBuilder.Entity<ThinkDeeper>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<CivicQuestion>(e =>
        {
            e.HasKey(q => q.Id);
            e.HasIndex(q => q.ExternalId).IsUnique();
            e.HasIndex(q => new { q.Type, q.Order });
            e.Property(q => q.Type).HasConversion<string>().HasMaxLength(40);
            e.OwnsMany(q => q.Choices, c =>
            {
                c.ToJson();
                c.OwnsMany(x => x.AxisDeltas);
            });
        });

        modelBuilder.Entity<CivicAnswer>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.UserId, a.QuestionId }).IsUnique();
            e.HasIndex(a => a.UserId);
            e.Property(a => a.Confidence).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Intensity).HasConversion<string>().HasMaxLength(20);
            e.HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UserId).IsUnique();
            e.OwnsMany(p => p.ArchetypeBlend, b =>
            {
                b.ToJson();
            });
            e.HasMany(p => p.AxisScores)
                .WithOne(s => s.UserProfile!)
                .HasForeignKey(s => s.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfileAxisScore>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.UserProfileId, s.AxisKey }).IsUnique();
        });

        modelBuilder.Entity<BudgetSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => new { s.UserId, s.CompletedAt });
            e.Ignore(s => s.TotalPoints);
            e.HasMany(s => s.Allocations)
                .WithOne(a => a.BudgetSession!)
                .HasForeignKey(a => a.BudgetSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetAllocation>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.BudgetSessionId, a.CategoryKey }).IsUnique();
        });

        modelBuilder.Entity<Election>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Scope, x.ScheduledAt });
            e.HasIndex(x => new { x.Scope, x.Region, x.ScheduledAt });
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<QuizQuestion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => x.Order);
        });

        modelBuilder.Entity<QuizResponse>(e =>
        {
            e.HasKey(x => x.Id);
            // The poll groups by question and filters by recency for the 60-day moving average.
            e.HasIndex(x => new { x.QuestionId, x.CreatedAt });
            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cohort>(e =>
        {
            e.HasKey(x => x.Id);
            // One cohort per (league, week); null AnchorLeagueId rows are distinct (solo cohorts).
            e.HasIndex(x => new { x.AnchorLeagueId, x.WeekKey }).IsUnique();
            e.HasMany(x => x.Members).WithOne(m => m.Cohort!).HasForeignKey(m => m.CohortId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CohortMember>(e =>
        {
            e.HasKey(x => x.Id);
            // Exactly one cohort per user per week.
            e.HasIndex(x => new { x.UserId, x.WeekKey }).IsUnique();
            e.HasIndex(x => x.CohortId);
        });

        modelBuilder.Entity<BillTimelineStep>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => x.Order);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<NewsItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => new { x.Status, x.IngestedAt });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Bill>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.HasIndex(x => new { x.SynthesisStatus, x.IngestedAt });
            e.HasIndex(x => new { x.Jurisdiction, x.LatestActionDate });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.SynthesisStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Jurisdiction).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.AxisPositions)
                .WithOne(p => p.Bill!)
                .HasForeignKey(p => p.BillId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillAxisPosition>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.BillId, p.AxisKey }).IsUnique();
        });

        modelBuilder.Entity<ValuesReceipt>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => new { r.UserId, r.CreatedAt });
            // Store the three string-list fields and the tensions as JSON columns.
            e.Property(r => r.LearnedInsights).HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            e.Property(r => r.ChangedAxes).HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            e.Property(r => r.UncertainAreas).HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            e.OwnsMany(r => r.Tensions, t =>
            {
                t.ToJson();
            });
        });

        modelBuilder.Entity<VirtualCandidate>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasIndex(c => new { c.Office, c.State, c.District });
            e.Property(c => c.Office).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.DefaultTone).HasConversion<string>().HasMaxLength(20);
            e.HasMany(c => c.AxisScores)
                .WithOne(s => s.Candidate!)
                .HasForeignKey(s => s.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.IssueTones)
                .WithOne(t => t.Candidate!)
                .HasForeignKey(t => t.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.PlatformPlanks)
                .WithOne(p => p.Candidate!)
                .HasForeignKey(p => p.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Sources)
                .WithOne(s => s.Candidate!)
                .HasForeignKey(s => s.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CandidateAxisScore>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.CandidateId, s.AxisKey }).IsUnique();
        });

        modelBuilder.Entity<CandidateIssueTone>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.CandidateId, t.Issue }).IsUnique();
            e.Property(t => t.Tone).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<PlatformPlank>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.CandidateId);
        });

        modelBuilder.Entity<CandidateSource>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.CandidateId, s.Priority });
            e.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<CampaignPost>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.CreatedAt).IsDescending();
            e.HasIndex(p => new { p.CandidateId, p.CreatedAt });
            e.HasIndex(p => p.TriggerBriefingSlug);
            // Feed tailoring: public posts (null owner) + a single user's own responses.
            e.HasIndex(p => new { p.OwnerUserId, p.CandidateId, p.CreatedAt });
            e.Property(p => p.Tone).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Trigger).HasConversion<string>().HasMaxLength(20);
            e.HasOne(p => p.Candidate)
                .WithMany()
                .HasForeignKey(p => p.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Fragments)
                .WithOne(f => f.Post!)
                .HasForeignKey(f => f.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PostFragment>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => new { f.PostId, f.Order });
        });

        modelBuilder.Entity<PostReaction>(e =>
        {
            e.HasKey(r => r.Id);
            // Idempotency: one reaction per user per (post, fragment?). Postgres
            // treats NULLs as distinct in unique indexes, so split into two
            // filtered indexes — one for the whole-post slot (FragmentId IS NULL)
            // and one for fragment reactions.
            e.HasIndex(r => new { r.UserId, r.PostId })
                .IsUnique()
                .HasFilter("\"FragmentId\" IS NULL");
            e.HasIndex(r => new { r.UserId, r.PostId, r.FragmentId })
                .IsUnique()
                .HasFilter("\"FragmentId\" IS NOT NULL");
            e.HasIndex(r => r.PostId);
            e.HasIndex(r => r.FragmentId);
            e.Property(r => r.Type).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<ElectionCycle>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasIndex(c => c.IsCurrent);
        });

        modelBuilder.Entity<CandidateFollow>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => new { f.UserId, f.CandidateId }).IsUnique();
        });

        modelBuilder.Entity<CandidateMute>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.UserId, m.CandidateId }).IsUnique();
        });

        modelBuilder.Entity<CivicCampaign>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.UserId);
            e.HasIndex(c => new { c.UserId, c.Status });
            e.Property(c => c.Difficulty).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(c => c.Candidate)
                .WithMany()
                .HasForeignKey(c => c.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Election)
                .WithMany()
                .HasForeignKey(c => c.ElectionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(c => c.Standings)
                .WithOne(s => s.Campaign!)
                .HasForeignKey(s => s.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(c => c.Weeks)
                .WithOne(w => w.Campaign!)
                .HasForeignKey(w => w.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(c => c.Actions)
                .WithOne(a => a.Campaign!)
                .HasForeignKey(a => a.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CivicCampaignStanding>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.CampaignId, s.CandidateId }).IsUnique();

            e.HasOne(s => s.Candidate)
                .WithMany()
                .HasForeignKey(s => s.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CivicCampaignWeek>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => new { w.CampaignId, w.DayNumber }).IsUnique();
        });

        modelBuilder.Entity<CivicCampaignAction>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.CampaignId, a.DayNumber });
            e.Property(a => a.ActionType).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.Tone).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<CandidateNewsResponse>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.CandidateId, r.BriefingSlug }).IsUnique();

            e.HasOne(r => r.Candidate)
                .WithMany()
                .HasForeignKey(r => r.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<League>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.OwnerUserId);
            // League names are unique per organizer (not globally): two different owners may both
            // have a "Friends League", but one owner can't reuse a name. The Id (GUID) stays the
            // global identifier.
            e.HasIndex(l => new { l.OwnerUserId, l.Name }).IsUnique();
            e.HasMany(l => l.Members)
                .WithOne(m => m.League!)
                .HasForeignKey(m => m.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(l => l.Rounds)
                .WithOne(r => r.League!)
                .HasForeignKey(r => r.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(l => l.Invites)
                .WithOne(i => i.League!)
                .HasForeignKey(i => i.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueMember>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.LeagueId, m.UserId }).IsUnique();
            e.HasIndex(m => new { m.LeagueId, m.Role });
            e.HasIndex(m => m.UserId);
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);

            // Linked campaign is optional and survives the campaign's deletion (member just unlinks).
            e.HasOne(m => m.Campaign)
                .WithMany()
                .HasForeignKey(m => m.CampaignId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(m => m.Candidate)
                .WithMany()
                .HasForeignKey(m => m.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(m => m.Entries)
                .WithOne(en => en.Member!)
                .HasForeignKey(en => en.LeagueMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueInvite>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Code).IsUnique();
            e.HasIndex(i => i.LeagueId);
            // Look up a pending personal invite by recipient within a league (dedupe on re-invite).
            e.HasIndex(i => new { i.LeagueId, i.Email });
        });

        modelBuilder.Entity<LeagueRound>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.LeagueId, r.SeasonNumber, r.RoundNumber }).IsUnique();
            e.HasIndex(r => new { r.LeagueId, r.Status });
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.PointsAwardedJson).HasColumnType("jsonb");

            // Winner is a soft pointer; clearing it on member removal must not cascade-delete the round.
            e.HasOne(r => r.WinnerMember)
                .WithMany()
                .HasForeignKey(r => r.WinnerMemberId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(r => r.Entries)
                .WithOne(en => en.Round!)
                .HasForeignKey(en => en.LeagueRoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeagueRoundEntry>(e =>
        {
            e.HasKey(en => en.Id);
            e.HasIndex(en => new { en.LeagueRoundId, en.LeagueMemberId }).IsUnique();
            e.HasIndex(en => en.PostId);
            e.Property(en => en.Tone).HasConversion<string>().HasMaxLength(20);

            e.HasOne(en => en.Candidate)
                .WithMany()
                .HasForeignKey(en => en.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            // The post holds the body + votes; deleting an entry removes its post.
            e.HasOne(en => en.Post)
                .WithMany()
                .HasForeignKey(en => en.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureCoalition(modelBuilder);
        ConfigureSocial(modelBuilder);
        ConfigureRoomGraph(modelBuilder);
        ConfigureRooms(modelBuilder);
    }

    /// <summary>
    /// Theme and Story Rooms, their revision history, and per-reader state (PRD 01, PRD 02).
    ///
    /// Rooms are table-per-hierarchy — one physical table with a "Kind" discriminator. It is
    /// the only inheritance in this context and it earns the exception: revisions, changelog,
    /// following and section progress are identical for both kinds, so one table lets
    /// RoomRevision.RoomId and UserRoomState.RoomId be real foreign keys to one target
    /// instead of a polymorphic pair on the hottest write path in the feature.
    /// </summary>
    private static void ConfigureRooms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Room>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasDiscriminator<string>("Kind")
                .HasValue<ThemeRoom>("Theme")
                .HasValue<StoryRoom>("Story");

            e.HasIndex(r => r.Slug).IsUnique();
            e.HasIndex(r => new { r.Status, r.LastMeaningfulUpdateAt });
            e.HasIndex(r => new { r.Status, r.Locality });

            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(r => r.Sensitivity).HasConversion<string>().HasMaxLength(20);

            // Both the admin controller and the correction propagator write rooms.
            // RoomRevisionService retries once on conflict rather than taking a lock.
            e.UseXminAsConcurrencyToken();

            e.OwnsMany(r => r.Provenance, p =>
            {
                p.ToJson();
                p.Property(x => x.ProposedBy).HasConversion<string>();
            });
        });

        modelBuilder.Entity<ThemeRoom>(e =>
        {
            e.Property(r => r.MonitoringCadence).HasConversion<string>().HasMaxLength(20);
            e.OwnsMany(r => r.EssentialFacts, f => f.ToJson());
            e.OwnsMany(r => r.TerminologyNotes, n => n.ToJson());
        });

        modelBuilder.Entity<StoryRoom>(e =>
        {
            e.Property(r => r.StoryType).HasConversion<string>().HasMaxLength(30);
            e.Property(r => r.TypePayloadJson).HasColumnType("jsonb");
            e.OwnsMany(r => r.WhyItMatters, d => d.ToJson());
            e.OwnsMany(r => r.Stakeholders, s => s.ToJson());
            e.OwnsMany(r => r.NextSteps, n => n.ToJson());
        });

        modelBuilder.Entity<RoomRevision>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.RoomId, r.Revision }).IsUnique();
            e.Property(r => r.SnapshotJson).HasColumnType("jsonb");

            e.OwnsMany(r => r.GateApprovals, g => g.ToJson());

            e.HasOne(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChangeLogEntry>(e =>
        {
            e.HasKey(c => c.Id);
            // The delta is a single indexed range scan on this — the hottest read here.
            e.HasIndex(c => new { c.RoomId, c.RevisionNumber });
            e.HasIndex(c => new { c.RoomId, c.IsMeaningful, c.CreatedAt });

            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.ObjectType).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.CorrectionKind).HasConversion<string>().HasMaxLength(20);

            e.HasOne(c => c.RoomRevision)
                .WithMany()
                .HasForeignKey(c => c.RoomRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRoomState>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.UserId, s.RoomId }).IsUnique();
            // The notify fan-out reads this.
            e.HasIndex(s => new { s.RoomId, s.Following });

            e.OwnsMany(s => s.SectionProgress, p => p.ToJson());

            e.HasOne(s => s.Room)
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserProfile>()
            .Property(p => p.RoomDensity).HasConversion<string>().HasMaxLength(10);

        ConfigureRoomContent(modelBuilder);
    }

    /// <summary>
    /// Actors, timeline events and developments — the content objects a room composes from.
    /// </summary>
    private static void ConfigureRoomContent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Actor>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Slug).IsUnique();
            e.HasIndex(a => a.ActorType);
            e.Property(a => a.ActorType).HasConversion<string>().HasMaxLength(30);

            e.OwnsMany(a => a.Provenance, p =>
            {
                p.ToJson();
                p.Property(x => x.ProposedBy).HasConversion<string>();
            });
        });

        modelBuilder.Entity<ActorRoomRole>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.RoomId, r.Tier, r.Ordinal });
            e.Property(r => r.Tier).HasConversion<string>().HasMaxLength(20);

            // One role per actor per room per decision. DecisionKey is nullable and null is
            // the common case (the room's default tiering), so this hits the Postgres
            // NULL-distinct trap head-on — a single unique index would happily accept two
            // default roles for the same actor. Split, exactly like DailyPuzzle's locality.
            e.HasIndex(r => new { r.RoomId, r.ActorId })
                .IsUnique()
                .HasFilter("\"DecisionKey\" IS NULL")
                .HasDatabaseName("IX_ActorRoomRoles_Room_Actor_Default");
            e.HasIndex(r => new { r.RoomId, r.ActorId, r.DecisionKey })
                .IsUnique()
                .HasFilter("\"DecisionKey\" IS NOT NULL")
                .HasDatabaseName("IX_ActorRoomRoles_Room_Actor_Decision");

            e.HasOne(r => r.Actor)
                .WithMany()
                .HasForeignKey(r => r.ActorId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Room)
                .WithMany()
                .HasForeignKey(r => r.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimelineEvent>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.RoomId, t.OccurredOn });
            e.Property(t => t.Marker).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.OccurredPrecision).HasConversion<string>().HasMaxLength(10);

            e.HasOne(t => t.Room)
                .WithMany()
                .HasForeignKey(t => t.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Development>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.RoomId, d.OccurredAt });
            e.Property(d => d.Category).HasConversion<string>().HasMaxLength(30);
            e.Property(d => d.EvidenceStatus).HasConversion<string>().HasMaxLength(30);

            e.HasOne(d => d.Room)
                .WithMany()
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Concept>()
            .Property(c => c.KnowledgeKind).HasConversion<string>().HasMaxLength(30);

        ConfigureRoomEditorial(modelBuilder);
        ConfigureRoomInteractions(modelBuilder);
        ConfigureMoneyTrail(modelBuilder);
    }

    /// <summary>
    /// The Money Trail (PRD 05). Every item carries all five ladder rungs, including
    /// the empty ones -- see MoneyMath.BuildLadder for why that is a data guarantee
    /// rather than a UI convention.
    /// </summary>
    private static void ConfigureMoneyTrail(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MoneyItem>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Slug).IsUnique();
            e.HasIndex(m => new { m.RoomId, m.CurrentStage });

            e.Property(m => m.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(m => m.CurrentStage).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.DollarBasis).HasConversion<string>().HasMaxLength(10);
            e.Property(m => m.AmountUsd).HasPrecision(18, 2);
            e.Property(m => m.AmountMinUsd).HasPrecision(18, 2);
            e.Property(m => m.AmountMaxUsd).HasPrecision(18, 2);

            e.OwnsMany(m => m.Breakdown, b => b.ToJson());
            e.OwnsMany(m => m.Comparisons, c => c.ToJson());
            e.OwnsMany(m => m.Provenance, p =>
            {
                p.ToJson();
                p.Property(x => x.ProposedBy).HasConversion<string>();
            });

            e.HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MoneyStageEntry>(e =>
        {
            e.HasKey(s => s.Id);
            // One row per stage per item -- exactly five, always.
            e.HasIndex(s => new { s.MoneyItemId, s.Stage }).IsUnique();
            e.Property(s => s.Stage).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.Applicability).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.AmountUsd).HasPrecision(18, 2);

            e.HasOne(s => s.MoneyItem)
                .WithMany()
                .HasForeignKey(s => s.MoneyItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Room interactions and calibrated predictions (PRD 06).
    /// </summary>
    private static void ConfigureRoomInteractions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Interaction>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Slug).IsUnique();
            e.HasIndex(i => new { i.RoomId, i.Ordinal });
            e.Property(i => i.PayloadJson).HasColumnType("jsonb");
            e.Property(i => i.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(i => i.ScoringMode).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Sensitivity).HasConversion<string>().HasMaxLength(20);
            e.Property(i => i.Status).HasConversion<string>().HasMaxLength(30);

            e.OwnsMany(i => i.Provenance, p =>
            {
                p.ToJson();
                p.Property(x => x.ProposedBy).HasConversion<string>();
            });

            e.HasOne(i => i.Room)
                .WithMany()
                .HasForeignKey(i => i.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoomInteractionPlay>(e =>
        {
            e.HasKey(p => p.Id);
            // Phase is part of the key because the two-phase interactions legitimately
            // store two rows per person -- that is the mechanic, not a duplicate. The
            // Post row doubles as the XP idempotency guard.
            e.HasIndex(p => new { p.InteractionId, p.UserId, p.Phase }).IsUnique();
            e.Property(p => p.Phase).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.ResponseJson).HasColumnType("jsonb");

            e.HasOne(p => p.Interaction)
                .WithMany()
                .HasForeignKey(p => p.InteractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Prediction>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasIndex(p => new { p.Outcome, p.ResolvesByAt });
            e.Property(p => p.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);

            e.HasOne(p => p.Room)
                .WithMany()
                .HasForeignKey(p => p.RoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserPrediction>(e =>
        {
            e.HasKey(u => u.Id);
            // One forecast per person per question, updatable until close.
            e.HasIndex(u => new { u.PredictionId, u.UserId }).IsUnique();
            e.HasIndex(u => new { u.UserId, u.CreatedAt });

            e.HasOne(u => u.Prediction)
                .WithMany()
                .HasForeignKey(u => u.PredictionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// Review flags and publish gates — the editorial machinery behind correction
    /// propagation (design 1y/1z, PRD 07).
    /// </summary>
    private static void ConfigureRoomEditorial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewFlag>(e =>
        {
            e.HasKey(f => f.Id);

            e.Property(f => f.ObjectType).HasConversion<string>().HasMaxLength(30);
            e.Property(f => f.TriggerObjectType).HasConversion<string>().HasMaxLength(30);
            e.Property(f => f.Reason).HasConversion<string>().HasMaxLength(30);
            e.Property(f => f.Action).HasConversion<string>().HasMaxLength(20);
            e.Property(f => f.Resolution).HasConversion<string>().HasMaxLength(20);

            // The review queue, oldest first, and the six-hour sweep both read this.
            e.HasIndex(f => new { f.ResolvedAt, f.CreatedAt });
            // The read path asks "is this object flagged?" on every render.
            e.HasIndex(f => new { f.ObjectType, f.ObjectId, f.ResolvedAt });

            // Re-running propagation must not spam the queue with duplicates. Filtered to
            // UNRESOLVED so the same object can legitimately be flagged again later for the
            // same reason by a subsequent correction.
            e.HasIndex(f => new { f.ObjectType, f.ObjectId, f.Reason, f.TriggerObjectId })
                .IsUnique()
                .HasFilter("\"ResolvedAt\" IS NULL")
                .HasDatabaseName("IX_ReviewFlags_Open_Unique");
        });

        modelBuilder.Entity<PublishGateResult>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Gate).HasConversion<string>().HasMaxLength(40);

            // Cleared per revision: editing after a sign-off re-opens the gate, because the
            // sign-off attested to text that no longer exists.
            e.HasIndex(g => new { g.RoomId, g.Gate, g.RoomRevision }).IsUnique();

            e.HasOne(g => g.Room)
                .WithMany()
                .HasForeignKey(g => g.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>
    /// The Topic Rooms knowledge graph (docs/Rooms Expansion, PRD 04).
    ///
    /// Claims, sources and the edges between them. Rooms themselves land in a later
    /// migration; the graph goes first because correction fan-out — the capability the
    /// whole feature rests on — is a property of the edge table, not of the rooms.
    /// </summary>
    private static void ConfigureRoomGraph(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ObjectLink>(e =>
        {
            e.HasKey(l => l.Id);

            e.Property(l => l.FromType).HasConversion<string>().HasMaxLength(30);
            e.Property(l => l.ToType).HasConversion<string>().HasMaxLength(30);
            e.Property(l => l.Relation).HasConversion<string>().HasMaxLength(40);
            e.Property(l => l.ProposedBy).HasConversion<string>().HasMaxLength(10);

            // "What does this object contain / cite?"
            e.HasIndex(l => new { l.FromType, l.FromId, l.Relation });

            // THE fan-out index. "Which objects depend on this claim?" is one scan here,
            // and that single query is why the graph is one polymorphic table instead of
            // two dozen typed join tables.
            e.HasIndex(l => new { l.ToType, l.ToId, l.Relation });

            // Idempotent attach — but only over OPEN edges. Retired edges (ValidTo set) are
            // deliberately allowed to repeat, because the same actor can join a committee,
            // leave, and rejoin. A plain unique index would forbid the rejoin.
            e.HasIndex(l => new { l.FromType, l.FromId, l.Relation, l.ToType, l.ToId })
                .IsUnique()
                .HasFilter("\"ValidTo\" IS NULL")
                .HasDatabaseName("IX_ObjectLinks_Open_Unique");
        });

        modelBuilder.Entity<SourceRef>(e =>
        {
            e.HasKey(s => s.Id);
            // Re-citing the same document must converge on one row, or a retraction cannot
            // find everything resting on it.
            e.HasIndex(s => s.UrlHash).IsUnique();
            e.HasIndex(s => new { s.SourceType, s.PublishedAt });
            // The withdrawal sweep (PRD 04 §14.3) scans by availability.
            e.HasIndex(s => s.Availability);

            e.Property(s => s.SourceType).HasConversion<string>().HasMaxLength(30);
            e.Property(s => s.Availability).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Claim>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Slug).IsUnique();
            // The extraction dedup key: two rooms describing the same fact converge here.
            e.HasIndex(c => c.NormalizedTextHash).IsUnique();
            // The ledger sorts least-settled first (design 1n).
            e.HasIndex(c => new { c.Status, c.LastReviewedAt });

            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);

            e.OwnsMany(c => c.Provenance, p =>
            {
                p.ToJson();
                p.Property(x => x.ProposedBy).HasConversion<string>();
            });
        });

        modelBuilder.Entity<ClaimStatusHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.HasIndex(h => new { h.ClaimId, h.ChangedAt });

            e.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(h => h.ChangeKind).HasConversion<string>().HasMaxLength(20);

            e.HasOne(h => h.Claim)
                .WithMany()
                .HasForeignKey(h => h.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>SocialPublisher's single writable table (shared engine). Mirrors the debate app's
    /// mapping: dedup index (content posted to a platform at most once; FeaturePost seeds exempt)
    /// + selector hot-path index.</summary>
    private static void ConfigureSocial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SocialPost>(e =>
        {
            e.Property(p => p.Platform).HasMaxLength(64);
            e.Property(p => p.Status).HasConversion<int>();
            e.Property(p => p.ContentType).HasConversion<int>();

            e.HasIndex(p => new { p.ContentType, p.ContentId, p.Platform })
                .IsUnique()
                .HasFilter("\"ContentId\" IS NOT NULL")
                .HasDatabaseName("IX_SocialPosts_Dedup");

            e.HasIndex(p => new { p.Status, p.NextRetryAt });
        });
    }

    /// <summary>
    /// Coalition game (Layer 0). The provision is the aggregate root; all child
    /// engagement cascades from it. Two non-tree edges (Amendment->Version and
    /// AcceptanceRecord->Version) are set non-cascading to avoid multiple
    /// cascade paths through the provision.
    /// </summary>
    private static void ConfigureCoalition(ModelBuilder modelBuilder)
    {
        // jsonb storage for the extracted sub-question-position vector. Stored
        // as jsonb (not fixed columns) precisely so a sub-question added after
        // birth needs no migration (principle A4).
        var positionsComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                   == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null).GetHashCode(),
            d => JsonSerializer.Deserialize<Dictionary<string, string>>(
                     JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                     (JsonSerializerOptions?)null) ?? new());

        modelBuilder.Entity<Provision>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasIndex(p => p.State);
            e.HasIndex(p => p.SourceBriefingId);
            e.Property(p => p.State).HasConversion<string>().HasMaxLength(20);

            e.HasMany(p => p.SubQuestions)
                .WithOne(s => s.Provision!)
                .HasForeignKey(s => s.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Positions)
                .WithOne(s => s.Provision!)
                .HasForeignKey(s => s.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Amendments)
                .WithOne(s => s.Provision!)
                .HasForeignKey(s => s.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Versions)
                .WithOne(s => s.Provision!)
                .HasForeignKey(s => s.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.AcceptanceRecords)
                .WithOne(s => s.Provision!)
                .HasForeignKey(s => s.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubQuestion>(e =>
        {
            e.HasKey(s => s.Id);
            // Key is stable + unique within a provision; it's the vector key.
            e.HasIndex(s => new { s.ProvisionId, s.Key }).IsUnique();
            e.Property(s => s.Origin).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<ProvisionPosition>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.ProvisionId);
            e.HasIndex(p => new { p.ProvisionId, p.UserId });
            e.Property(p => p.Intensity).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Amendment>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.ProvisionId);
            // Proposed version is a soft pointer; deleting the version nulls it
            // rather than cascading (the provision is the cascade root).
            e.HasOne(a => a.ProposedVersion)
                .WithMany()
                .HasForeignKey(a => a.ProposedVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProvisionVersion>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.ProvisionId);
            e.HasIndex(v => new { v.ProvisionId, v.TextHash });
            e.Property(v => v.ExtractedPositions)
                .HasColumnType("jsonb")
                .HasConversion(
                    d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
                    s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, (JsonSerializerOptions?)null) ?? new())
                .Metadata.SetValueComparer(positionsComparer);
        });

        modelBuilder.Entity<AcceptanceRecord>(e =>
        {
            e.HasKey(r => r.Id);
            // One acceptance record per (user, version).
            e.HasIndex(r => new { r.UserId, r.VersionId }).IsUnique();
            e.HasIndex(r => r.ProvisionId);
            e.HasIndex(r => r.VersionId);
            e.Property(r => r.Intensity).HasConversion<string>().HasMaxLength(20);
            // Version edge is non-cascading: the provision cascade already
            // removes these rows, so this avoids a second cascade path.
            e.HasOne(r => r.Version)
                .WithMany(v => v.AcceptanceRecords)
                .HasForeignKey(r => r.VersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExtractionCacheEntry>(e =>
        {
            e.HasKey(c => c.Id);
            // Cache key: normalized-text hash + known-sub-question signature.
            e.HasIndex(c => new { c.TextHash, c.KnownSignature }).IsUnique();
            e.Property(c => c.ResultJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<CoalitionParticipant>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.ProvisionId, c.UserId }).IsUnique();
            e.Property(c => c.RegionJson).HasColumnType("jsonb");
            e.Property(c => c.IntensitiesJson).HasColumnType("jsonb");
            e.HasOne(c => c.Provision)
                .WithMany()
                .HasForeignKey(c => c.ProvisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CoalitionCircle>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasMany(l => l.Members)
                .WithOne(m => m.Circle!)
                .HasForeignKey(m => m.CircleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CoalitionCircleMember>(e =>
        {
            e.HasKey(m => m.Id);
            // A user belongs to at most one coalition circle.
            e.HasIndex(m => m.UserId).IsUnique();
        });

        modelBuilder.Entity<CoalitionActivityDay>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.UserId, a.Day }).IsUnique();
        });

        modelBuilder.Entity<CoalitionAct>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.UserId, a.CreatedAt });
            e.HasIndex(a => new { a.ProvisionId, a.Type });
            e.Property(a => a.Type).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<DailyPuzzle>(e =>
        {
            e.HasKey(p => p.Id);
            // One puzzle per kind per day per locality — the generator relies on this to
            // stay idempotent when two passes (or two instances) race.
            //
            // Split into two FILTERED indexes on purpose: Postgres treats NULLs as
            // distinct in a unique index, so a single (Kind, PuzzleDate, Locality) index
            // would happily accept two national puzzles for the same day — exactly the
            // case that matters, since national is the default.
            e.HasIndex(p => new { p.Kind, p.PuzzleDate })
                .IsUnique()
                .HasFilter("\"Locality\" IS NULL")
                .HasDatabaseName("IX_DailyPuzzles_Kind_PuzzleDate_National");
            e.HasIndex(p => new { p.Kind, p.PuzzleDate, p.Locality })
                .IsUnique()
                .HasFilter("\"Locality\" IS NOT NULL")
                .HasDatabaseName("IX_DailyPuzzles_Kind_PuzzleDate_Locality");
            e.HasIndex(p => new { p.Kind, p.Status, p.PuzzleDate });
            e.Property(p => p.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<DailyPuzzlePlay>(e =>
        {
            e.HasKey(p => p.Id);
            // One play per person per puzzle. This is also the idempotency guard for the
            // XP award — see DailyPuzzleService.AwardAsync.
            e.HasIndex(p => new { p.PuzzleId, p.UserId }).IsUnique();
            e.HasIndex(p => new { p.UserId, p.CreatedAt });
            e.HasOne(p => p.Puzzle)
                .WithMany()
                .HasForeignKey(p => p.PuzzleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
