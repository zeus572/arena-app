using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Models;

namespace Arena.API.Services;

public class BotHeartbeatService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILlmService _llm;
    private readonly TopicGeneratorService _topics;
    private readonly BudgetService _budget;
    private readonly ILogger<BotHeartbeatService> _logger;
    private readonly int _intervalSeconds;
    private readonly int _maxActiveDebates;
    private readonly int _turnsPerDebate;
    private readonly int _turnDelaySeconds;

    public BotHeartbeatService(
        IServiceScopeFactory scopeFactory,
        ILlmService llm,
        TopicGeneratorService topics,
        BudgetService budget,
        IConfiguration config,
        ILogger<BotHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory;
        _llm = llm;
        _topics = topics;
        _budget = budget;
        _logger = logger;
        _intervalSeconds = config.GetValue("BotHeartbeat:IntervalSeconds", 300);
        _maxActiveDebates = config.GetValue("BotHeartbeat:MaxActiveDebates", 5);
        _turnsPerDebate = config.GetValue("BotHeartbeat:TurnsPerDebate", 6);
        _turnDelaySeconds = config.GetValue("BotHeartbeat:TurnDelaySeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotHeartbeat started. Interval={Interval}s, MaxActive={Max}, TurnsPerDebate={Turns}",
            _intervalSeconds, _maxActiveDebates, _turnsPerDebate);

        // Initial delay to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHeartbeatAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "BotHeartbeat tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();

        _logger.LogInformation("BotHeartbeat tick starting...");

        // 1. Activate pending debates older than 1 minute
        var pendingCutoff = DateTime.UtcNow.AddMinutes(-1);
        var pendingDebates = await db.Debates
            .Where(d => d.Status == DebateStatus.Pending && d.CreatedAt < pendingCutoff)
            .ToListAsync(ct);

        foreach (var debate in pendingDebates)
        {
            debate.Status = DebateStatus.Active;
            debate.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Activated debate '{Topic}' ({Id})", debate.Topic, debate.Id);
        }
        if (pendingDebates.Count > 0)
            await db.SaveChangesAsync(ct);

        // 2. Create new debates if below max
        var activeCount = await db.Debates.CountAsync(d => d.Status == DebateStatus.Active, ct);
        if (activeCount < _maxActiveDebates)
        {
            var agents = await db.Agents.ToListAsync(ct);
            if (agents.Count >= 2)
            {
                var topic = _topics.PickRandomTopic();
                var (proponentId, opponentId) = _topics.PickAgentPair(agents);

                var newDebate = new Debate
                {
                    Id = Guid.NewGuid(),
                    Topic = topic,
                    Status = DebateStatus.Active,
                    ProponentId = proponentId,
                    OpponentId = opponentId,
                };

                db.Debates.Add(newDebate);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Created new debate: '{Topic}'", topic);
            }
        }

        // 3. Generate turns for active debates
        var activeDebates = await db.Debates
            .Where(d => d.Status == DebateStatus.Active)
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .Include(d => d.Turns.OrderBy(t => t.TurnNumber))
            .ToListAsync(ct);

        foreach (var debate in activeDebates)
        {
            if (debate.Turns.Count >= _turnsPerDebate)
            {
                // Complete the debate
                debate.Status = DebateStatus.Completed;
                debate.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Completed debate '{Topic}' ({Id})", debate.Topic, debate.Id);

                // Update agent reputation based on votes
                await UpdateReputationAsync(db, debate, ct);
                continue;
            }

            // Determine whose turn it is (alternating, proponent starts)
            var nextTurnNumber = debate.Turns.Count + 1;
            var isProponentTurn = nextTurnNumber % 2 == 1;
            var agent = isProponentTurn ? debate.Proponent : debate.Opponent;

            if (!_budget.CanGenerateTurn(agent.Id))
            {
                _logger.LogWarning("Agent {Agent} has hit daily budget limit, skipping", agent.Name);
                continue;
            }

            try
            {
                var content = await _llm.GenerateTurnAsync(agent, debate, debate.Turns.ToList());

                var turn = new Turn
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = agent.Id,
                    TurnNumber = nextTurnNumber,
                    Content = content,
                };

                db.Turns.Add(turn);
                await db.SaveChangesAsync(ct);
                _budget.RecordTurn(agent.Id);

                _logger.LogInformation("Generated turn {Num} by {Agent} for '{Topic}'",
                    nextTurnNumber, agent.Name, debate.Topic);

                // Pace turns with a delay
                if (_turnDelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(_turnDelaySeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to generate turn for debate {DebateId}", debate.Id);
            }
        }

        _logger.LogInformation("BotHeartbeat tick complete. Active debates: {Count}", activeDebates.Count);
    }

    private static async Task UpdateReputationAsync(ArenaDbContext db, Debate debate, CancellationToken ct)
    {
        var proponentVotes = await db.Votes.CountAsync(v => v.DebateId == debate.Id && v.VotedForAgentId == debate.ProponentId, ct);
        var opponentVotes = await db.Votes.CountAsync(v => v.DebateId == debate.Id && v.VotedForAgentId == debate.OpponentId, ct);

        if (proponentVotes == opponentVotes) return;

        var winner = proponentVotes > opponentVotes ? debate.Proponent : debate.Opponent;
        var loser = proponentVotes > opponentVotes ? debate.Opponent : debate.Proponent;

        winner.ReputationScore = Math.Min(100, winner.ReputationScore + 0.1);
        loser.ReputationScore = Math.Max(0, loser.ReputationScore - 0.05);

        await db.SaveChangesAsync(ct);
    }
}
