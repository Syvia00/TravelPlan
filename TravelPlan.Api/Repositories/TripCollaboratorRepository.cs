using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class TripCollaboratorRepository : Repository<TripCollaborator>, ITripCollaboratorRepository
{
    public TripCollaboratorRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<TripCollaborator>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default) =>
        DbSet.Where(c => c.TripId == tripId)
            .Include(c => c.User)
            .OrderBy(c => c.InvitedAt)
            .ToListAsync(cancellationToken);

    public Task<TripCollaborator?> FindAsync(int tripId, int userId, CancellationToken cancellationToken = default) =>
        DbSet.SingleOrDefaultAsync(c => c.TripId == tripId && c.UserId == userId, cancellationToken);
}
