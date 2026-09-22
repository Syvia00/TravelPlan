using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class AccommodationRepository : Repository<Accommodation>, IAccommodationRepository
{
    public AccommodationRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<Accommodation>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(a => a.TripId == tripId)
            .OrderBy(a => a.CheckIn)
            .ToListAsync(cancellationToken);
}
