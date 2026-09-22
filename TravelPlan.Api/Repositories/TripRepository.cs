using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Repositories;

public class TripRepository : Repository<Trip>, ITripRepository
{
    public TripRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<List<Trip>> ListByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        DbSet.Where(t => t.UserId == userId || t.Collaborators.Any(c => c.UserId == userId && c.AcceptedAt != null))
            // Filtered Include: at most one row per trip (this user's own collaborator row, if
            // any) — lets the controller read the caller's role without a query per trip.
            .Include(t => t.Collaborators.Where(c => c.UserId == userId && c.AcceptedAt != null))
            .OrderByDescending(t => t.StartDate)
            .ToListAsync(cancellationToken);

    public Task<List<Trip>> ListDueForCompletionAsync(DateOnly asOf, CancellationToken cancellationToken = default) =>
        DbSet.Where(t => t.EndDate < asOf && t.Status != TripStatus.Completed && t.Status != TripStatus.Cancelled)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryMarkCompletedAsync(int tripId, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var affected = await DbSet
            .Where(t => t.Id == tripId && t.Status != TripStatus.Completed && t.Status != TripStatus.Cancelled)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.Status, TripStatus.Completed)
                    .SetProperty(t => t.UpdatedAt, updatedAtUtc),
                cancellationToken);

        return affected > 0;
    }
}
