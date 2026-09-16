using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class TravelLegRepository : Repository<TravelLeg>, ITravelLegRepository
{
    public TravelLegRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<TravelLeg>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(l => l.TripId == tripId)
            .OrderBy(l => l.DepartureTime)
            .ToListAsync(cancellationToken);

    public Task<TravelLeg?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default) =>
        DbSet.Where(l => l.Id == id && l.Trip!.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
}
