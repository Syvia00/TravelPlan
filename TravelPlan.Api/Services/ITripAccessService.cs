using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services;

/// <summary>
/// The single authority every controller/service asks "can the current request touch trip X at
/// role Y" — the trip owner, an accepted TripCollaborator, and a valid share-link visitor all
/// funnel through here. Nothing else re-implements this check; when the access model changes,
/// this is the only place that needs to.
/// </summary>
public interface ITripAccessService
{
    /// <summary>
    /// True if the current request has at least <paramref name="minimumRole"/> access to the
    /// trip: its owner (any role), an accepted collaborator whose Role is at least
    /// <paramref name="minimumRole"/>, or a share-link visitor scoped to this exact trip whose
    /// link Role is at least <paramref name="minimumRole"/>.
    /// </summary>
    Task<bool> HasAccessAsync(int tripId, TripRole minimumRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// True only for the trip's owner — used to gate trip deletion and collaborator/share-link
    /// management, which even an Editor collaborator must not be able to do.
    /// </summary>
    Task<bool> IsOwnerAsync(int tripId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ownership/role summary for a single trip, for building a TripDto — null if the current
    /// request has no access at all. IsOwner true implies Role is meaningless (an owner isn't a
    /// "Viewer" or "Editor", they're everything); Role is only set for a non-owner.
    /// </summary>
    Task<TripAccessInfo?> GetAccessInfoAsync(int tripId, CancellationToken cancellationToken = default);
}

public readonly record struct TripAccessInfo(bool IsOwner, TripRole? Role);
