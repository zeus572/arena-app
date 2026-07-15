using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Civic.API.Models;
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
    }
}
