namespace TravelPlan.Api.Services;

/// <summary>
/// Periodically refreshes the ExchangeRates cache from the FX provider — the only thing that ever
/// calls IExchangeRateService.RefreshRatesAsync. ExchangeRatesController's conversion endpoint only
/// ever reads the cache this keeps warm, so budget screens never wait on (or depend on the uptime
/// of) the external provider.
/// </summary>
public class ExchangeRateRefreshBackgroundService : BackgroundService
{
    // Frankfurter itself only publishes new rates once a day (weekdays, ECB reference rates around
    // 16:00 CET) — hourly is already far more frequent than the data actually changes. Configure
    // via "ExchangeRateRefreshIntervalMinutes" — see deployment-runbook.md.
    private const int DefaultRefreshIntervalMinutes = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeRateRefreshBackgroundService> _logger;
    private readonly TimeSpan _refreshInterval;

    public ExchangeRateRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExchangeRateRefreshBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var minutes = configuration.GetValue("ExchangeRateRefreshIntervalMinutes", DefaultRefreshIntervalMinutes);
        _refreshInterval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var exchangeRates = scope.ServiceProvider.GetRequiredService<IExchangeRateService>();

        try
        {
            await exchangeRates.RefreshRatesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exchange rate refresh failed.");
        }
    }
}
