namespace Arena.API.Services;

public class DailyTopicRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyTopicRefreshService> _logger;
    private readonly int _intervalHours;

    public DailyTopicRefreshService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<DailyTopicRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _intervalHours = config.GetValue("TopicRefresh:IntervalHours", 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyTopicRefresh started. Interval={Hours}h", _intervalHours);

        // Initial delay — let the app start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var newsService = scope.ServiceProvider.GetRequiredService<NewsTopicService>();
                await newsService.GenerateTopicsFromNewsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "DailyTopicRefresh tick failed");
            }

            await Task.Delay(TimeSpan.FromHours(_intervalHours), stoppingToken);
        }
    }
}
