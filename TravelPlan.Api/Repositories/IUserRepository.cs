using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IUserRepository : IRepository<User>
{
    /// <summary>Case-insensitive lookup by email.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an existing user by email (any state — signed up or still a pending placeholder), or
    /// creates a new pending placeholder row (ExternalAuthId null) if none exists. Used only by
    /// TripCollaboratorsController's invite-by-email flow — UserSyncMiddleware later claims the
    /// placeholder (attaches a real ExternalAuthId) the moment that email actually signs in.
    /// </summary>
    Task<User> FindOrCreatePendingAsync(string email, CancellationToken cancellationToken = default);
}
