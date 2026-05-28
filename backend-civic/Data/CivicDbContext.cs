using Microsoft.EntityFrameworkCore;
using Civic.API.Models;

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
    public DbSet<BillTimelineStep> BillTimelineSteps => Set<BillTimelineStep>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();

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
    }
}
