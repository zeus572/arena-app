using Microsoft.EntityFrameworkCore;
using Arena.API.Models;
using Arena.API.Models.Social;

namespace Arena.API.Data;

public class ArenaDbContext : DbContext
{
    public ArenaDbContext(DbContextOptions<ArenaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Debate> Debates => Set<Debate>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<DebateAggregate> DebateAggregates => Set<DebateAggregate>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DebateTag> DebateTags => Set<DebateTag>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TopicProposal> TopicProposals => Set<TopicProposal>();
    public DbSet<TopicVote> TopicVotes => Set<TopicVote>();
    public DbSet<GeneratedTopic> GeneratedTopics => Set<GeneratedTopic>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Intervention> Interventions => Set<Intervention>();
    public DbSet<AgentSource> AgentSources => Set<AgentSource>();
    public DbSet<DebateParticipant> DebateParticipants => Set<DebateParticipant>();
    public DbSet<DebateArena> Arenas => Set<DebateArena>();
    public DbSet<BudgetFact> BudgetFacts => Set<BudgetFact>();
    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();
    public DbSet<EmailSuppression> EmailSuppressions => Set<EmailSuppression>();
    public DbSet<EmailSendLog> EmailSendLogs => Set<EmailSendLog>();
    public DbSet<MfaBackupCode> MfaBackupCodes => Set<MfaBackupCode>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => new { u.AuthProvider, u.ExternalId });
        });

        modelBuilder.Entity<Agent>(e =>
        {
            e.HasIndex(a => a.Name).IsUnique();
        });

        modelBuilder.Entity<Debate>(e =>
        {
            e.HasOne(d => d.Proponent)
                .WithMany(a => a.DebatesAsProponent)
                .HasForeignKey(d => d.ProponentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.Opponent)
                .WithMany(a => a.DebatesAsOpponent)
                .HasForeignKey(d => d.OpponentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.GeneratedTopic)
                .WithMany()
                .HasForeignKey(d => d.GeneratedTopicId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(d => d.Arena)
                .WithMany(a => a.Debates)
                .HasForeignKey(d => d.ArenaId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(d => d.ForkedFromDebate)
                .WithMany(d => d.Forks)
                .HasForeignKey(d => d.ForkedFromDebateId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(d => d.ArenaId);
            e.HasIndex(d => d.ForkedFromDebateId);
        });

        modelBuilder.Entity<DebateArena>(e =>
        {
            e.HasIndex(a => a.Slug).IsUnique();
        });

        modelBuilder.Entity<Turn>(e =>
        {
            e.HasIndex(t => new { t.DebateId, t.TurnNumber }).IsUnique();
        });

        modelBuilder.Entity<Vote>(e =>
        {
            e.HasIndex(v => new { v.DebateId, v.UserId }).IsUnique();
        });

        modelBuilder.Entity<DebateAggregate>(e =>
        {
            e.HasIndex(a => new { a.DebateId, a.AggregateDate }).IsUnique();
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<TopicVote>(e =>
        {
            e.HasIndex(tv => new { tv.TopicProposalId, tv.UserId }).IsUnique();
        });

        modelBuilder.Entity<Prediction>(e =>
        {
            e.HasIndex(p => new { p.DebateId, p.UserId }).IsUnique();
        });

        modelBuilder.Entity<DebateParticipant>(e =>
        {
            e.HasOne(dp => dp.Debate)
                .WithMany(d => d.Participants)
                .HasForeignKey(dp => dp.DebateId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(dp => dp.Agent)
                .WithMany()
                .HasForeignKey(dp => dp.AgentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebateTag>(e =>
        {
            e.HasKey(dt => new { dt.DebateId, dt.TagId });

            e.HasOne(dt => dt.Debate)
                .WithMany(d => d.DebateTags)
                .HasForeignKey(dt => dt.DebateId);

            e.HasOne(dt => dt.Tag)
                .WithMany(t => t.DebateTags)
                .HasForeignKey(dt => dt.TagId);
        });

        modelBuilder.Entity<BudgetFact>(e =>
        {
            e.HasIndex(f => new { f.FactDate, f.IsActive });
        });

        modelBuilder.Entity<AccountToken>(e =>
        {
            e.HasIndex(t => t.TokenHash);
            e.HasIndex(t => new { t.UserId, t.Purpose });
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailSuppression>(e =>
        {
            e.HasIndex(s => s.Email).IsUnique();
        });

        modelBuilder.Entity<EmailSendLog>(e =>
        {
            e.HasIndex(l => new { l.Email, l.SentAt });
        });

        modelBuilder.Entity<MfaBackupCode>(e =>
        {
            e.HasIndex(c => c.CodeHash);
            e.HasIndex(c => c.UserId);
            e.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrustedDevice>(e =>
        {
            e.HasIndex(d => d.TokenHash);
            e.HasIndex(d => d.UserId);
            e.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SocialPost>(e =>
        {
            e.Property(p => p.Platform).HasMaxLength(64);
            e.Property(p => p.Status).HasConversion<int>();
            e.Property(p => p.ContentType).HasConversion<int>();

            // Dedup (§5): a piece of content may be posted to a platform at most once.
            // Filtered so that FeaturePost seeds (ContentId IS NULL) are exempt and can coexist.
            // The filter predicate quotes the column with " which is valid on both PostgreSQL
            // (the prod provider) and SQLite (the Gate 1 test provider), so the partial index
            // is enforced in both.
            e.HasIndex(p => new { p.ContentType, p.ContentId, p.Platform })
                .IsUnique()
                .HasFilter("\"ContentId\" IS NOT NULL")
                .HasDatabaseName("IX_SocialPosts_Dedup");

            // Selector hot path: filter by status + retry gate.
            e.HasIndex(p => new { p.Status, p.NextRetryAt });
        });
    }
}
