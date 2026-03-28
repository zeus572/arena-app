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
    private readonly HeartbeatSettings _settings;
    private readonly ILogger<BotHeartbeatService> _logger;
    private readonly int _maxActiveDebates;
    private readonly int _turnsPerDebate;
    private readonly int _turnDelaySeconds;
    private readonly int _compromiseTurns;

    public BotHeartbeatService(
        IServiceScopeFactory scopeFactory,
        ILlmService llm,
        TopicGeneratorService topics,
        BudgetService budget,
        HeartbeatSettings settings,
        IConfiguration config,
        ILogger<BotHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory;
        _llm = llm;
        _topics = topics;
        _budget = budget;
        _settings = settings;
        _logger = logger;
        _maxActiveDebates = config.GetValue("BotHeartbeat:MaxActiveDebates", 5);
        _turnsPerDebate = config.GetValue("BotHeartbeat:TurnsPerDebate", 6);
        _turnDelaySeconds = config.GetValue("BotHeartbeat:TurnDelaySeconds", 30);
        _compromiseTurns = config.GetValue("BotHeartbeat:CompromiseTurns", 2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotHeartbeat started. Enabled={Enabled}, Interval={Interval}s, MaxActive={Max}, TurnsPerDebate={Turns}, CompromiseTurns={Compromise}",
            _settings.Enabled, _settings.IntervalSeconds, _maxActiveDebates, _turnsPerDebate, _compromiseTurns);

        // Initial delay to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_settings.Enabled)
            {
                try
                {
                    await RunHeartbeatAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "BotHeartbeat tick failed");
                }
            }
            else
            {
                _logger.LogDebug("BotHeartbeat is disabled, skipping tick");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), stoppingToken);
        }
    }

    public async Task RunHeartbeatAsync(CancellationToken ct)
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
                var topic = await _topics.PickRandomTopicAsync();
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

                var tagging = scope.ServiceProvider.GetRequiredService<TaggingService>();
                await tagging.ExtractAndAssignTagsAsync(db, newDebate);

                _logger.LogInformation("Created new debate: '{Topic}'", topic);
            }
        }

        // 3. Generate turns for active and compromising debates
        var debates = await db.Debates
            .Where(d => d.Status == DebateStatus.Active || d.Status == DebateStatus.Compromising)
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .Include(d => d.Turns.OrderBy(t => t.TurnNumber))
            .ToListAsync(ct);

        foreach (var debate in debates)
        {
            var argumentTurns = debate.Turns.Where(t => t.Type == TurnType.Argument).ToList();
            var compromiseTurns = debate.Turns.Where(t => t.Type == TurnType.Compromise).ToList();

            // Check if active debate should transition to compromise phase
            if (debate.Status == DebateStatus.Active && argumentTurns.Count >= _turnsPerDebate)
            {
                debate.Status = DebateStatus.Compromising;
                debate.UpdatedAt = DateTime.UtcNow;

                // Insert arbiter announcement turn
                var arbiterTurn = new Turn
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = debate.ProponentId,
                    TurnNumber = debate.Turns.Count + 1,
                    Type = TurnType.Arbiter,
                    Content = "**The Arbiter has intervened.** Both debaters are now directed to find common ground and propose a compromise, including a concrete budget proposal that both sides can accept.",
                };

                db.Turns.Add(arbiterTurn);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Debate '{Topic}' entering compromise phase", debate.Topic);
                continue;
            }

            // Check if compromising debate is done — both agents must have at least one compromise turn
            var bothAgentsCompromised = compromiseTurns.Any(t => t.AgentId == debate.ProponentId)
                                    && compromiseTurns.Any(t => t.AgentId == debate.OpponentId);
            if (debate.Status == DebateStatus.Compromising && compromiseTurns.Count >= _compromiseTurns && bothAgentsCompromised)
            {
                // Insert arbiter closing turn
                var closingTurn = new Turn
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = debate.ProponentId,
                    TurnNumber = debate.Turns.Count + 1,
                    Type = TurnType.Arbiter,
                    Content = "**The Arbiter has closed this debate.** Both sides have presented their compromise proposals. The debate is now complete — cast your vote for the most compelling argument.",
                };
                db.Turns.Add(closingTurn);

                debate.Status = DebateStatus.Completed;
                debate.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Completed debate '{Topic}' ({Id})", debate.Topic, debate.Id);

                await UpdateReputationAsync(db, debate, ct);
                await ResolvePredictionsAsync(db, debate, ct);
                continue;
            }

            // Generate next turn
            var nextTurnNumber = debate.Turns.Count + 1;
            var isProponentTurn = nextTurnNumber % 2 == 1;
            var agent = isProponentTurn ? debate.Proponent : debate.Opponent;
            var turnType = debate.Status == DebateStatus.Compromising
                ? TurnType.Compromise
                : TurnType.Argument;

            if (!_budget.CanGenerateTurn(agent.Id))
            {
                _logger.LogWarning("Agent {Agent} has hit daily budget limit, skipping", agent.Name);
                continue;
            }

            try
            {
                var result = await _llm.GenerateTurnAsync(agent, debate, debate.Turns.ToList(), turnType);

                var turn = new Turn
                {
                    Id = Guid.NewGuid(),
                    DebateId = debate.Id,
                    AgentId = agent.Id,
                    TurnNumber = nextTurnNumber,
                    Type = turnType,
                    Content = result.Content,
                    CitationsJson = result.Citations.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(result.Citations)
                        : null,
                };

                db.Turns.Add(turn);
                await db.SaveChangesAsync(ct);
                _budget.RecordTurn(agent.Id);

                _logger.LogInformation("Generated {TurnType} turn {Num} by {Agent} for '{Topic}'",
                    turnType, nextTurnNumber, agent.Name, debate.Topic);

                // Pace turns with a delay
                if (_turnDelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(_turnDelaySeconds), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to generate turn for debate {DebateId}", debate.Id);
            }
        }

        _logger.LogInformation("BotHeartbeat tick complete. Active/Compromising debates: {Count}", debates.Count);
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

    private static async Task ResolvePredictionsAsync(ArenaDbContext db, Debate debate, CancellationToken ct)
    {
        var proponentVotes = await db.Votes.CountAsync(v => v.DebateId == debate.Id && v.VotedForAgentId == debate.ProponentId, ct);
        var opponentVotes = await db.Votes.CountAsync(v => v.DebateId == debate.Id && v.VotedForAgentId == debate.OpponentId, ct);

        if (proponentVotes == opponentVotes) return; // draw — no resolution

        var winnerId = proponentVotes > opponentVotes ? debate.ProponentId : debate.OpponentId;

        var predictions = await db.Predictions
            .Where(p => p.DebateId == debate.Id && p.IsCorrect == null)
            .ToListAsync(ct);

        foreach (var prediction in predictions)
        {
            prediction.IsCorrect = prediction.PredictedAgentId == winnerId;
        }

        await db.SaveChangesAsync(ct);
    }
}
