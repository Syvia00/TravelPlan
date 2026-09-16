using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class DestinationRepository : Repository<Destination>, IDestinationRepository
{
    public DestinationRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<Destination>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(d => d.TripId == tripId)
            .OrderBy(d => d.EntryDate)
            .ToListAsync(cancellationToken);
}
