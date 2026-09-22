using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IPlanItemRepository : IRepository<PlanItem>
{
    Task<List<PlanItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
