using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITravelLegRepository : IRepository<TravelLeg>
{
    Task<List<TravelLeg>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
