using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services;

public class TripCompletionService : ITripCompletionService
{
    private readonly ITripRepository _trips;
    private readonly ITripReportService _reportService;
    private readonly ILogger<TripCompletionService> _logger;

    public TripCompletionService(ITripRepository trips, ITripReportService reportService, ILogger<TripCompletionService> logger)
    {
        _trips = trips;
        _reportService = reportService;
        _logger = logger;
    }

    public async Task<Trip?> CompleteTripAsync(int tripId, int userId, CancellationToken cancellationToken = default)
    {
        var trip = await _trips.GetByIdForUserAsync(tripId, userId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        if (trip.Status != TripStatus.Completed)
        {
            await TryCompleteAsync(trip, cancellationToken);
        }

        return trip;
    }

    public async Task<int> CompleteDueTripsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var due = await _trips.ListDueForCompletionAsync(today, cancellationToken);

        var completed = 0;
        foreach (var trip in due)
        {
            try
            {
                if (await TryCompleteAsync(trip, cancellationToken))
                {
                    completed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-complete trip {TripId}.", trip.Id);
            }
        }

        return completed;
    }

    /// <summary>
    /// Wins or loses the completion race atomically via TryMarkCompletedAsync (a conditional
    /// UPDATE the database evaluates at execution time, not against this possibly-stale
    /// in-memory `trip`). Only the winner generates the MemorySummary report — with multiple
    /// App Service instances running this same sweep concurrently, exactly one of them will see
    /// its UPDATE affect a row; the rest see 0 rows affected and back off without generating a
    /// duplicate report. Returns whether this call won.
    /// </summary>
    private async Task<bool> TryCompleteAsync(Trip trip, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var won = await _trips.TryMarkCompletedAsync(trip.Id, now, cancellationToken);
        if (!won)
        {
            return false;
        }

        // ExecuteUpdateAsync bypasses the change tracker, so reflect the win in the in-memory
        // copy too — callers (e.g. the controller building a TripDto) read `trip` afterward.
        trip.Status = TripStatus.Completed;
        trip.UpdatedAt = now;

        await _reportService.GenerateReportForTripAsync(
            trip, TripMemoryReportType.MemorySummary, null, null, cancellationToken);

        return true;
    }
}
