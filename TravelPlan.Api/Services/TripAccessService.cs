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
            return linkTripId == tripId && _currentUser.ShareLinkRole is { } linkRole && linkRole.SatisfiesMinimum(minimumRole);
        }

        if (_currentUser.UserId is not { } userId)
        {
            return false;
        }

        // Project the raw role out (same shape as GetAccessInfoAsync below) and compare via
        // SatisfiesMinimum in memory, after EF has converted it back to the enum — not inside the
        // query below, where a comparison operator would get translated straight into SQL. Role is
        // stored as int (see TripCollaboratorConfiguration), so that would be numerically correct
        // today, but keeping the comparison in memory means it stays correct even if the storage
        // representation ever changes again, instead of depending on remembering why it matters.
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
            return false;
        }

        return trip.UserId == userId || (trip.CollaboratorRole is { } role && role.SatisfiesMinimum(minimumRole));
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
