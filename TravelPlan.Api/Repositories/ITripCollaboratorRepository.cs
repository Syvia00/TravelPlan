using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface ITripCollaboratorRepository : IRepository<TripCollaborator>
{
    Task<List<TripCollaborator>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Used to prevent inviting the same user to the same trip twice.</summary>
    Task<TripCollaborator?> FindAsync(int tripId, int userId, CancellationToken cancellationToken = default);
}
