using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripShareLinkRepository : IRepository<TripShareLink>
{
    Task<List<TripShareLink>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
