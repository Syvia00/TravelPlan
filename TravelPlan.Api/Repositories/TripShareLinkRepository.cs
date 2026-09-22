using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class TripShareLinkRepository : Repository<TripShareLink>, ITripShareLinkRepository
{
    public TripShareLinkRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<TripShareLink>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(l => l.TripId == tripId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
}
