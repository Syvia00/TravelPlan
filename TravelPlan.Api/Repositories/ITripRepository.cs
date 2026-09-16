using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripRepository : IRepository<Trip>
{
    Task<List<Trip>> ListByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<Trip?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);

    /// <summary>Trips (any user) whose EndDate has passed and aren't already Completed or
    /// Cancelled — used by the auto-completion background sweep.</summary>
    Task<List<Trip>> ListDueForCompletionAsync(DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions a trip to Completed — a single conditional UPDATE (via
    /// ExecuteUpdateAsync, bypassing the change tracker) whose WHERE clause re-checks Status is
    /// still not Completed/Cancelled at the moment the database applies it. Returns true only
    /// for the caller that actually won the transition; false if the trip was already
    /// Completed/Cancelled (by this call or a concurrent one). Safe under concurrent callers —
    /// e.g. multiple App Service instances running the same background sweep — since the
    /// database, not an in-memory read, is the source of truth for the WHERE check.
    /// </summary>
    Task<bool> TryMarkCompletedAsync(int tripId, DateTime updatedAtUtc, CancellationToken cancellationToken = default);
}
