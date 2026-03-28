using Microsoft.EntityFrameworkCore;
using Arena.API.Models;

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
    }
}
