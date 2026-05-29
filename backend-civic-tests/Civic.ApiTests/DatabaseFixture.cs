using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Civic.API.Data;
using Xunit;

namespace Civic.ApiTests;

/// <summary>
/// One-time setup per test collection: build the factory (which runs migrations + seed on
/// the civic_test DB), then provide a Respawner that resets mutable tables between tests.
/// Read-only catalog tables (Briefings/Concepts/ThinkDeepers) are NOT reset so the seeded
/// data persists across all tests in the collection.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    public CivicApiFactory Factory { get; } = new();
    private Respawner? _respawner;

    public async Task InitializeAsync()
    {
        // Force the host to build so Program.cs runs MigrateAsync + SeedAsync against civic_test.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
            await db.Database.MigrateAsync();
        }

        await using var conn = new NpgsqlConnection(CivicApiFactory.TestConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Respawn.Graph.Table[]
            {
                new("Briefings"),
                new("BriefingWordsToKnow"),
                new("Concepts"),
                new("ThinkDeepers"),
                new("CivicQuestions"),
                new("Elections"),
                new("QuizQuestions"),
                new("BillTimelineSteps"),
                // Virtual Candidate catalog (seeded once, treated as read-only).
                new("VirtualCandidates"),
                new("CandidateAxisScores"),
                new("CandidateIssueTones"),
                new("PlatformPlanks"),
                new("CandidateSources"),
                new("ElectionCycles"),
                new("__EFMigrationsHistory"),
            },
        });
    }

    public async Task ResetMutableAsync()
    {
        if (_respawner is null) return;
        await using var conn = new NpgsqlConnection(CivicApiFactory.TestConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
