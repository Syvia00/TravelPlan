using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripMemoryRepository : IRepository<TripMemory>
{
    Task<List<TripMemory>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
