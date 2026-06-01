using Arena.API.Data;
using Arena.API.Models;
using Arena.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arena.API.Tests;

/// <summary>
/// A deterministic, offline ILlmService used by the campaign service tests so no network is ever hit.
/// </summary>
public sealed class FakeLlmService : ILlmService
{
    public int TurnCalls { get; private set; }
    public int CommentaryCalls { get; private set; }
    public string FixedContent { get; init; } = "FAKE_LLM_TURN_CONTENT";

    public Task<LlmTurnResult> GenerateTurnAsync(
        Agent agent, Debate debate, List<Turn> previousTurns,
        TurnType turnType = TurnType.Argument, string? crowdQuestion = null, Agent? opponent = null)
    {
        TurnCalls++;
        return Task.FromResult(new LlmTurnResult { Content = FixedContent });
    }

    public Task<CommentaryResult> GenerateCommentaryAsync(
        Agent commentatorA, Agent commentatorB, Debate debate, List<Turn> previousTurns)
    {
        CommentaryCalls++;
        return Task.FromResult(new CommentaryResult());
    }
}

/// <summary>
/// Test harness for <see cref="CampaignService"/> over an in-memory <see cref="ArenaDbContext"/>.
///
/// One context per harness, backed by a unique in-memory database. Each logical "request"
/// (a <see cref="Run{T}"/> call, or a <see cref="Query{TEntity}"/> read) clears the change tracker
/// first, mirroring the per-HTTP-request scoping the service receives in production (where every
/// request resolves a fresh scoped DbContext starting with an empty tracker).
///
/// We deliberately keep a SINGLE context rather than opening a new context instance per call against
/// the same named in-memory database: the EF Core InMemory provider maintains a per-context identity
/// map that, across short-lived sibling contexts on a shared store, can intermittently report
/// "Attempted to update or delete an entity that does not exist in the store" on SaveChanges. That is
/// a test-host artifact of the InMemory provider, not a behavior of the relational (PostgreSQL)
/// provider used in production. Clearing the tracker on one context reproduces request-scoped
/// behavior faithfully and deterministically.
/// </summary>
public sealed class CampaignTestHarness : IDisposable
{
    private readonly ArenaDbContext _db;

    public CampaignTuningOptions Tuning { get; }
    public FakeLlmService Llm { get; }
    public IConfiguration Config { get; }

    public CampaignTestHarness(
        CampaignTuningOptions? tuning = null, FakeLlmService? llm = null, IConfiguration? config = null)
    {
        Tuning = tuning ?? new CampaignTuningOptions();
        Llm = llm ?? new FakeLlmService();
        Config = config ?? EmptyApiKeyConfig();

        var options = new DbContextOptionsBuilder<ArenaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        _db = new ArenaDbContext(options);
    }

    private CampaignService NewService() =>
        new(_db, Llm, Config, Options.Create(Tuning), NullLogger<CampaignService>.Instance);

    /// <summary>Run one unit of work as a single request: empty tracker before, detach all after.</summary>
    public async Task<T> Run<T>(Func<CampaignService, ArenaDbContext, Task<T>> work)
    {
        _db.ChangeTracker.Clear();
        var result = await work(NewService(), _db);
        _db.ChangeTracker.Clear();
        return result;
    }

    public async Task Run(Func<CampaignService, ArenaDbContext, Task> work)
    {
        _db.ChangeTracker.Clear();
        await work(NewService(), _db);
        _db.ChangeTracker.Clear();
    }

    /// <summary>Open a no-tracking query against the store for assertions.</summary>
    public IQueryable<TEntity> Query<TEntity>() where TEntity : class
    {
        _db.ChangeTracker.Clear();
        return _db.Set<TEntity>().AsNoTracking();
    }

    public static IConfiguration EmptyApiKeyConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "",
            })
            .Build();

    /// <summary>Seed and persist a campaign-owner user, returning it (detached).</summary>
    public User SeedUser()
    {
        _db.ChangeTracker.Clear();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"owner-{Guid.NewGuid():N}"[..16],
            Email = $"{Guid.NewGuid():N}@arena.local",
            IsAnonymous = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return user;
    }

    public void Dispose() => _db.Dispose();
}
