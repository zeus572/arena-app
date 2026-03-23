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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
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
    }
}
