using Arena.API.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Arena.UnitTests.Social;

/// <summary>
/// Builds an ArenaDbContext backed by an in-memory SQLite database.
///
/// We use SQLite (not the EF InMemory provider) wherever a test relies on real
/// relational behaviour — most importantly UNIQUE-index enforcement, which the
/// InMemory provider silently ignores. SQLite honours the filtered unique dedup
/// index, so Gate 1 actually exercises the constraint.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<ArenaDbContext> Options { get; }

    public SqliteTestDb()
    {
        // A single shared open connection keeps the in-memory DB alive for the test's lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ArenaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new ArenaDbContext(Options);
        ctx.Database.EnsureCreated();
    }

    public ArenaDbContext NewContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
