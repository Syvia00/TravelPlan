using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITravelLegRepository : IRepository<TravelLeg>
{
    Task<List<TravelLeg>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Fetches a travel leg and checks ownership through its parent Trip in one query.</summary>
    Task<TravelLeg?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}
