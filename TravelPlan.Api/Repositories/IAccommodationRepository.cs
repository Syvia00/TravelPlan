using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IAccommodationRepository : IRepository<Accommodation>
{
    Task<List<Accommodation>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
