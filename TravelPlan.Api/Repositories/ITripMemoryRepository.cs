using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripMemoryRepository : IRepository<TripMemory>
{
    Task<List<TripMemory>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Fetches a report and checks ownership through its parent Trip in one query.</summary>
    Task<TripMemory?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}
