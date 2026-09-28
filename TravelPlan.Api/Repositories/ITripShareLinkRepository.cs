using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripShareLinkRepository : IRepository<TripShareLink>
{
    Task<List<TripShareLink>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Used to resolve/claim a link from its token — the visitor never knows its row id.</summary>
    Task<TripShareLink?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
}
