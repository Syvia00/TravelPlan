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
    // Dev-friendly cadence — cheap to run (a handful of trips at most, most sweeps a no-op) and
    // frequent enough that a trip completes promptly after its EndDate passes without needing a
    // real scheduler (Azure Functions timer trigger, etc.), which is Phase 2+ infrastructure.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripCompletionBackgroundService> _logger;

    public TripCompletionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TripCompletionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
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
