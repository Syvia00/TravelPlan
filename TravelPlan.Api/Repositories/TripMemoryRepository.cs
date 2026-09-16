using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class TripMemoryRepository : Repository<TripMemory>, ITripMemoryRepository
{
    public TripMemoryRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<TripMemory>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(m => m.TripId == tripId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<TripMemory?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default) =>
        DbSet.Where(m => m.Id == id && m.Trip!.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);
}
