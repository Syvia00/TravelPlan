using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IAccommodationRepository : IRepository<Accommodation>
{
    Task<List<Accommodation>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Fetches an accommodation and checks ownership through its parent Trip in one query.</summary>
    Task<Accommodation?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}
