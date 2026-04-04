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
    private readonly int _turnDelaySeconds;

    // Weighted format selection for bot-generated debates
    private static readonly (string format, double weight)[] FormatWeights =
    {
        ("standard", 0.40),
        ("common_ground", 0.15),
        ("tweet", 0.15),
        ("rapid_fire", 0.10),
        ("longform", 0.05),
        ("roast", 0.10),
        ("town_hall", 0.05),
    };

    // Curated common ground pairings (agent GUIDs)
    private static readonly (string, string)[] CommonGroundPairings =
    {
        ("a1a00000-0000-0000-0000-000000000101", "a1a00000-0000-0000-0000-000000000103"), // Trump + Sanders
        ("a1a00000-0000-0000-0000-000000000102", "a1a00000-0000-0000-0000-000000000106"), // Obama + Haley
        ("a1a00000-0000-0000-0000-000000000202", "a1a00000-0000-0000-0000-000000000104"), // Jefferson + AOC
        ("a1a00000-0000-0000-0000-000000000204", "a1a00000-0000-0000-0000-000000000103"), // Hamilton + Sanders
        ("a1a00000-0000-0000-0000-000000000205", "a1a00000-0000-0000-0000-000000000208"), // Lincoln + MLK
        ("a1a00000-0000-0000-0000-000000000203", "a1a00000-0000-0000-0000-000000000101"), // Franklin + Trump
        ("a1a00000-0000-0000-0000-000000000201", "a1a00000-0000-0000-0000-000000000105"), // Washington + DeSantis
    };

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
        _turnDelaySeconds = config.GetValue("BotHeartbeat:TurnDelaySeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BotHeartbeat started. Enabled={Enabled}, Interval={Interval}s, MaxActive={Max}",
            _settings.Enabled, _settings.IntervalSeconds, _maxActiveDebates);

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
            await CreateNewDebateAsync(db, ct);
        }

        // 3. Generate turns for active and compromising debates
        var debates = await db.Debates
            .Where(d => d.Status == DebateStatus.Active || d.Status == DebateStatus.Compromising)
            .Include(d => d.Proponent)
            .Include(d => d.Opponent)
            .Include(d => d.Turns.OrderBy(t => t.TurnNumber))
            .Include(d => d.Participants).ThenInclude(p => p.Agent)
            .ToListAsync(ct);

        foreach (var debate in debates)
        {
            await ProcessDebateTurnAsync(db, debate, ct);
        }

        _logger.LogInformation("BotHeartbeat tick complete. Active/Compromising debates: {Count}", debates.Count);
    }

    private async Task CreateNewDebateAsync(ArenaDbContext db, CancellationToken ct)
    {
        var agents = await db.Agents
            .Where(a => !a.IsCommentator)
            .ToListAsync(ct);

        if (agents.Count < 2) return;

        var format = PickRandomFormat();
        var config = DebateFormatConfig.Get(format);

        // Pick topic
        var newsGeneratedTopic = await db.GeneratedTopics
            .Where(t => !t.Used && t.Source == "news")
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        string topic;
        var source = "bot";

        if (newsGeneratedTopic != null)
        {
            topic = newsGeneratedTopic.Title;
            newsGeneratedTopic.Used = true;
            source = "breaking";
        }
        else
        {
            topic = await _topics.PickRandomTopicAsync();
        }

        // Pick agents based on format
        var debateAgents = agents.Where(a => !a.IsWildcard).ToList();
        Guid proponentId, opponentId;

        if (format == "common_ground")
        {
            // Prefer curated pairings
            var pair = PickCommonGroundPairing(debateAgents);
            proponentId = pair.proponent;
            opponentId = pair.opponent;
        }
        else
        {
            var (p, o) = _topics.PickAgentPair(debateAgents);
            proponentId = p;
            opponentId = o;
        }

        var newDebate = new Debate
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            Format = format,
            Status = DebateStatus.Active,
            ProponentId = proponentId,
            OpponentId = opponentId,
            Source = source,
            GeneratedTopicId = newsGeneratedTopic?.Id,
        };

        db.Debates.Add(newDebate);

        // For town hall, add questioner participants
        if (format == "town_hall")
        {
            var questioners = debateAgents
                .Where(a => a.Id != proponentId && a.Id != opponentId)
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .ToList();

            for (var i = 0; i < questioners.Count; i++)
            {
                db.DebateParticipants.Add(new DebateParticipant
                {
                    Id = Guid.NewGuid(),
                    DebateId = newDebate.Id,
                    AgentId = questioners[i].Id,
                    Role = "questioner",
                    QuestionOrder = i + 1,
                });
            }

            // Mark proponent as respondent
            db.DebateParticipants.Add(new DebateParticipant
            {
                Id = Guid.NewGuid(),
                DebateId = newDebate.Id,
                AgentId = proponentId,
                Role = "respondent",
                QuestionOrder = 0,
            });
        }

        await db.SaveChangesAsync(ct);

        var tagging = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<TaggingService>();
        await tagging.ExtractAndAssignTagsAsync(db, newDebate);

        _logger.LogInformation("Created new {Format} {Source} debate: '{Topic}'", format, source, topic);
    }

    private async Task ProcessDebateTurnAsync(ArenaDbContext db, Debate debate, CancellationToken ct)
    {
        var config = DebateFormatConfig.Get(debate.Format);
        var argumentTurns = debate.Turns.Where(t => t.Type == TurnType.Argument || t.Type == TurnType.Agreement || t.Type == TurnType.Roast || t.Type == TurnType.Question).ToList();
        var compromiseTurns = debate.Turns.Where(t => t.Type == TurnType.Compromise).ToList();

        // Check if active debate should transition to compromise phase (standard format only)
        if (debate.Status == DebateStatus.Active && config.HasCompromisePhase && argumentTurns.Count >= config.MaxTurns - 2)
        {
            debate.Status = DebateStatus.Compromising;
            debate.UpdatedAt = DateTime.UtcNow;

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
            return;
        }

        // Check if compromising debate is done
        var bothAgentsCompromised = compromiseTurns.Any(t => t.AgentId == debate.ProponentId)
                                && compromiseTurns.Any(t => t.AgentId == debate.OpponentId);
        if (debate.Status == DebateStatus.Compromising && compromiseTurns.Count >= 2 && bothAgentsCompromised)
        {
            await CompleteDebateAsync(db, debate, ct);
            return;
        }

        // Check if non-compromise debate has hit max turns
        if (debate.Status == DebateStatus.Active && !config.HasCompromisePhase && argumentTurns.Count >= config.MaxTurns)
        {
            await CompleteDebateAsync(db, debate, ct);
            return;
        }

        // Generate next turn
        var nextTurnNumber = debate.Turns.Count + 1;
        Agent agent;
        TurnType turnType;

        if (debate.Status == DebateStatus.Compromising)
        {
            var isProponentTurn = nextTurnNumber % 2 == 1;
            agent = isProponentTurn ? debate.Proponent : debate.Opponent;
            turnType = TurnType.Compromise;
        }
        else if (debate.Format == "town_hall")
        {
            // Alternating Q&A: odd turns = question, even turns = answer
            var isQuestionTurn = nextTurnNumber % 2 == 1;
            if (isQuestionTurn)
            {
                // Pick next questioner
                var questionerIdx = (nextTurnNumber / 2) % Math.Max(1, debate.Participants.Count(p => p.Role == "questioner"));
                var questioner = debate.Participants
                    .Where(p => p.Role == "questioner")
                    .OrderBy(p => p.QuestionOrder)
                    .Skip(questionerIdx)
                    .FirstOrDefault();

                agent = questioner?.Agent ?? debate.Opponent;
                turnType = TurnType.Question;
            }
            else
            {
                agent = debate.Proponent; // respondent
                turnType = TurnType.Argument;
            }
        }
        else
        {
            var isProponentTurn = nextTurnNumber % 2 == 1;
            agent = isProponentTurn ? debate.Proponent : debate.Opponent;
            turnType = debate.Format switch
            {
                "common_ground" => TurnType.Agreement,
                "roast" => TurnType.Roast,
                _ => TurnType.Argument,
            };
        }

        if (!_budget.CanGenerateTurn(agent.Id))
        {
            _logger.LogWarning("Agent {Agent} has hit daily budget limit, skipping", agent.Name);
            return;
        }

        try
        {
            // Check for crowd interventions
            string? crowdQuestion = null;
            if (debate.Format is "standard" or "longform" or "town_hall")
            {
                var topIntervention = await db.Interventions
                    .Where(i => i.DebateId == debate.Id && !i.Used)
                    .OrderByDescending(i => i.Upvotes)
                    .ThenBy(i => i.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (topIntervention != null && topIntervention.Upvotes >= 1)
                {
                    crowdQuestion = topIntervention.Content;
                    topIntervention.Used = true;
                    topIntervention.UsedInTurnNumber = nextTurnNumber;
                }
            }

            var opponent = agent.Id == debate.ProponentId ? debate.Opponent : debate.Proponent;
            var result = await _llm.GenerateTurnAsync(agent, debate, debate.Turns.ToList(), turnType, crowdQuestion, opponent);

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

            _logger.LogInformation("Generated {TurnType} turn {Num} by {Agent} for '{Topic}' ({Format})",
                turnType, nextTurnNumber, agent.Name, debate.Topic, debate.Format);

            // Wildcard injection (format-aware)
            if (config.HasWildcards && debate.Status == DebateStatus.Active
                && nextTurnNumber >= config.WildcardStartTurn
                && Random.Shared.NextDouble() < config.WildcardChance)
            {
                await InjectWildcardAsync(db, debate, ct);
            }

            // Commentary injection (format-aware)
            if (config.HasCommentary)
            {
                await TryInjectCommentaryAsync(db, debate, config, ct);
            }

            // Pace turns with format-aware delay
            var delay = config.TurnDelaySeconds;
            if (delay > 0)
                await Task.Delay(TimeSpan.FromSeconds(delay), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to generate turn for debate {DebateId}", debate.Id);
        }
    }

    private async Task InjectWildcardAsync(ArenaDbContext db, Debate debate, CancellationToken ct)
    {
        var wildcardAgent = await db.Agents
            .Where(a => a.IsWildcard)
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync(ct);

        if (wildcardAgent == null) return;

        var wildcardResult = await _llm.GenerateTurnAsync(
            wildcardAgent, debate, debate.Turns.ToList(), TurnType.Wildcard);

        var wildcardTurn = new Turn
        {
            Id = Guid.NewGuid(),
            DebateId = debate.Id,
            AgentId = wildcardAgent.Id,
            TurnNumber = debate.Turns.Count + 1,
            Type = TurnType.Wildcard,
            Content = wildcardResult.Content,
            CitationsJson = wildcardResult.Citations.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(wildcardResult.Citations)
                : null,
        };

        db.Turns.Add(wildcardTurn);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Wildcard '{Agent}' injected into '{Topic}'", wildcardAgent.Name, debate.Topic);
    }

    private async Task TryInjectCommentaryAsync(ArenaDbContext db, Debate debate, DebateFormatConfig config, CancellationToken ct)
    {
        var allTurns = await db.Turns
            .Where(t => t.DebateId == debate.Id)
            .Include(t => t.Agent)
            .OrderBy(t => t.TurnNumber)
            .ToListAsync(ct);

        var contentTurns = allTurns.Count(t => t.Type != TurnType.Commentary && t.Type != TurnType.Arbiter);
        var commentaryPairs = allTurns.Count(t => t.Type == TurnType.Commentary) / 2;
        var expectedPairs = contentTurns / config.CommentaryEveryNTurns;

        if (commentaryPairs >= expectedPairs || contentTurns < config.CommentaryEveryNTurns) return;

        var commentators = await db.Agents
            .Where(a => a.IsCommentator)
            .OrderBy(a => a.Name)
            .Take(2)
            .ToListAsync(ct);

        if (commentators.Count < 2) return;

        try
        {
            var commentary = await _llm.GenerateCommentaryAsync(
                commentators[0], commentators[1], debate, allTurns);

            var baseTurnNumber = allTurns.Count + 1;

            db.Turns.Add(new Turn
            {
                Id = Guid.NewGuid(),
                DebateId = debate.Id,
                AgentId = commentators[0].Id,
                TurnNumber = baseTurnNumber,
                Type = TurnType.Commentary,
                Content = commentary.CommentatorAContent,
            });

            db.Turns.Add(new Turn
            {
                Id = Guid.NewGuid(),
                DebateId = debate.Id,
                AgentId = commentators[1].Id,
                TurnNumber = baseTurnNumber + 1,
                Type = TurnType.Commentary,
                Content = commentary.CommentatorBContent,
            });

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Commentary injected into '{Topic}' after {Args} content turns",
                debate.Topic, contentTurns);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate commentary for debate {DebateId}", debate.Id);
        }
    }

    private async Task CompleteDebateAsync(ArenaDbContext db, Debate debate, CancellationToken ct)
    {
        var closingTurn = new Turn
        {
            Id = Guid.NewGuid(),
            DebateId = debate.Id,
            AgentId = debate.ProponentId,
            TurnNumber = debate.Turns.Count + 1,
            Type = TurnType.Arbiter,
            Content = debate.Format == "common_ground"
                ? "**The debate is now complete.** Both sides have explored their areas of agreement. The audience can now weigh in — was this common ground genuine or performative?"
                : "**The Arbiter has closed this debate.** Both sides have presented their arguments. The debate is now complete — cast your vote for the most compelling argument.",
        };
        db.Turns.Add(closingTurn);

        debate.Status = DebateStatus.Completed;
        debate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Completed {Format} debate '{Topic}' ({Id})", debate.Format, debate.Topic, debate.Id);

        await UpdateReputationAsync(db, debate, ct);
        await ResolvePredictionsAsync(db, debate, ct);
    }

    private static string PickRandomFormat()
    {
        var roll = Random.Shared.NextDouble();
        var cumulative = 0.0;
        foreach (var (format, weight) in FormatWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
                return format;
        }
        return "standard";
    }

    private static (Guid proponent, Guid opponent) PickCommonGroundPairing(List<Agent> available)
    {
        var availableIds = available.Select(a => a.Id.ToString()).ToHashSet();

        // Shuffle curated pairings and find one where both agents are available
        var shuffled = CommonGroundPairings.OrderBy(_ => Random.Shared.Next()).ToList();
        foreach (var (a, b) in shuffled)
        {
            if (availableIds.Contains(a) && availableIds.Contains(b))
                return (Guid.Parse(a), Guid.Parse(b));
        }

        // Fallback: pick any two agents with high trait deltas
        var sorted = available.OrderBy(_ => Guid.NewGuid()).Take(2).ToList();
        return (sorted[0].Id, sorted[1].Id);
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

        if (proponentVotes == opponentVotes) return;

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
