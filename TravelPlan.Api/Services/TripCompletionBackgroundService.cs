namespace TravelPlan.Api.Services;

/// <summary>
/// Periodically marks trips Completed once their EndDate has passed, and generates each one's
/// MemorySummary report — the "auto-trigger on EndDate passing" half of trip completion; the
/// other half is the user-driven POST /api/trips/{id}/complete action. Both funnel through
/// ITripCompletionService so there's exactly one place that decides what "completing a trip"
/// means.
/// </summary>
public class TripCompletionBackgroundService : BackgroundService
{
    // Default is deliberately a few hours, not minutes. This sweep runs inside the API container,
    // so it only ticks while a replica is actually up — on Container Apps Consumption (minReplicas
    // 0) the whole app scales to zero when idle, taking this loop down with it. While a replica IS
    // up (because of real traffic), a short interval would reset Azure SQL serverless's idle timer
    // on every tick and never let the database reach its 1-hour auto-pause threshold — this used to
    // be a fixed 15 minutes, which burned money for no benefit while there are no real users to
    // complete trips promptly for. Configure via "CompletionSweepIntervalMinutes" once near-real-time
    // completion actually matters (i.e. once real users exist) — see deployment-runbook.md.
    private const int DefaultCheckIntervalMinutes = 240;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripCompletionBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public TripCompletionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TripCompletionBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutes = configuration.GetValue("CompletionSweepIntervalMinutes", DefaultCheckIntervalMinutes);
        _checkInterval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var completionService = scope.ServiceProvider.GetRequiredService<ITripCompletionService>();

        try
        {
            var completed = await completionService.CompleteDueTripsAsync(cancellationToken);
            if (completed > 0)
            {
                _logger.LogInformation("Auto-completed {Count} trip(s) whose EndDate had passed.", completed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trip auto-completion sweep failed.");
        }
    }
}
