using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Services;

public interface ITripCompletionService
{
    /// <summary>
    /// Marks a trip Completed and generates its MemorySummary report. Idempotent — if the trip
    /// is already Completed, no new report is generated (erd.md: a memory summary is only ever
    /// generated once). Returns null if the trip doesn't exist or isn't owned by the user.
    /// </summary>
    Task<Trip?> CompleteTripAsync(int tripId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sweeps every trip (any user) whose EndDate has passed and isn't already Completed or
    /// Cancelled, marking each Completed and generating its MemorySummary report. Used by
    /// TripCompletionBackgroundService. Returns how many trips were completed.
    /// </summary>
    Task<int> CompleteDueTripsAsync(CancellationToken cancellationToken = default);
}
