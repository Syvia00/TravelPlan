using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class PlanItemRepository : Repository<PlanItem>, IPlanItemRepository
{
    public PlanItemRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<PlanItem>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(p => p.TripId == tripId)
            .OrderBy(p => p.Date)
            .ThenBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<PlanItem?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default) =>
        DbSet.Where(p => p.Id == id && p.Trip!.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
}
