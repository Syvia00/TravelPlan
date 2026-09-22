using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IBudgetItemRepository : IRepository<BudgetItem>
{
    Task<List<BudgetItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
