using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public interface IBudgetItemRepository : IRepository<BudgetItem>
{
    Task<List<BudgetItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>Fetches a budget item and checks ownership through its parent Trip in one query.</summary>
    Task<BudgetItem?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}
