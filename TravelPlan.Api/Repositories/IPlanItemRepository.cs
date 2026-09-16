using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IPlanItemRepository : IRepository<PlanItem>
{
    Task<List<PlanItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Fetches a plan item and checks ownership through its parent Trip in one query.</summary>
    Task<PlanItem?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}
