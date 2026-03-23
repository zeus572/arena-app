using System.Collections.Concurrent;

namespace Arena.API.Services;

public class BudgetService
{
    private readonly ConcurrentDictionary<string, int> _dailyCounts = new();
    private DateOnly _currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
    private readonly int _maxTurnsPerAgentPerDay;

    public BudgetService(IConfiguration config)
    {
        _maxTurnsPerAgentPerDay = config.GetValue("BotHeartbeat:MaxTurnsPerAgentPerDay", 50);
    }

    public bool CanGenerateTurn(Guid agentId)
    {
        ResetIfNewDay();
        var key = $"{_currentDate}:{agentId}";
        var count = _dailyCounts.GetOrAdd(key, 0);
        return count < _maxTurnsPerAgentPerDay;
    }

    public void RecordTurn(Guid agentId)
    {
        ResetIfNewDay();
        var key = $"{_currentDate}:{agentId}";
        _dailyCounts.AddOrUpdate(key, 1, (_, c) => c + 1);
    }

    private void ResetIfNewDay()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today != _currentDate)
        {
            _dailyCounts.Clear();
            _currentDate = today;
        }
    }
}
