using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class BudgetItemRepository : Repository<BudgetItem>, IBudgetItemRepository
{
    public BudgetItemRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<BudgetItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(b => b.TripId == tripId)
            .OrderBy(b => b.Date)
            .ToListAsync(cancellationToken);

    public Task<BudgetItem?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default) =>
        DbSet.Where(b => b.Id == id && b.Trip!.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
}
