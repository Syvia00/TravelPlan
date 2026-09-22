using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services;

public class TripAccessService : ITripAccessService
{
    private readonly TravelPlanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TripAccessService(TravelPlanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<bool> HasAccessAsync(int tripId, TripRole minimumRole, CancellationToken cancellationToken = default)
    {
        // Share-link visitors have no UserId — their identity IS the link, scoped to exactly one
        // trip. No DB round trip needed: the link was already validated (found, unexpired) when
        // ShareLinkAuthHandler authenticated the request.
        if (_currentUser.ShareLinkTripId is { } linkTripId)
        {
            return linkTripId == tripId && _currentUser.ShareLinkRole is { } linkRole && linkRole >= minimumRole;
        }

        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        return await _context.Trips.AnyAsync(
            t => t.Id == tripId &&
                 (t.UserId == userId ||
                  t.Collaborators.Any(c => c.UserId == userId && c.AcceptedAt != null && c.Role >= minimumRole)),
            cancellationToken);
    }

    public async Task<bool> IsOwnerAsync(int tripId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        return await _context.Trips.AnyAsync(t => t.Id == tripId && t.UserId == userId, cancellationToken);
    }

    public async Task<TripAccessInfo?> GetAccessInfoAsync(int tripId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.ShareLinkTripId is { } linkTripId)
        {
            return linkTripId == tripId && _currentUser.ShareLinkRole is { } linkRole
                ? new TripAccessInfo(false, linkRole)
                : null;
        }

        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        var trip = await _context.Trips
            .Where(t => t.Id == tripId)
            .Select(t => new
            {
                t.UserId,
                CollaboratorRole = t.Collaborators
                    .Where(c => c.UserId == userId && c.AcceptedAt != null)
                    .Select(c => (TripRole?)c.Role)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (trip is null)
        {
            return null;
        }

        if (trip.UserId == userId)
        {
            return new TripAccessInfo(true, null);
        }

        return trip.CollaboratorRole is { } role ? new TripAccessInfo(false, role) : null;
    }
}
