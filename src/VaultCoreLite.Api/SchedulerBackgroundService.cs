using VaultCoreLite.Application.Services;

namespace VaultCoreLite.Api;

public sealed class SchedulerWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 100;
}

public sealed class SchedulerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SchedulerBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SchedulerBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SchedulerBackgroundService> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _configuration.GetSection("SchedulerWorker").Get<SchedulerWorkerOptions>() ?? new SchedulerWorkerOptions();
        if (!options.Enabled)
        {
            _logger.LogInformation("Scheduler worker disabled by configuration.");
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Clamp(options.PollIntervalSeconds, 1, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _semaphore.WaitAsync(TimeSpan.Zero, stoppingToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scheduler = scope.ServiceProvider.GetRequiredService<SchedulerService>();
                        var processed = await scheduler.RunDueAsync(DateTimeOffset.UtcNow, options.BatchSize, stoppingToken);
                        if (processed > 0) _logger.LogInformation("Processed {Count} due schedules.", processed);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                else
                {
                    _logger.LogWarning("Previous scheduler tick still running; skipping this poll.");
                }
                await Task.Delay(poll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler worker failed; retrying after {Delay}.", poll);
                await Task.Delay(poll, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _semaphore.Dispose();
        base.Dispose();
    }
}
