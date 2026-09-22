using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services;

/// <summary>
/// Resolved by UserSyncMiddleware from the authenticated request — either a real signed-in user
/// (JWT, UserId set) or an anonymous share-link visitor (ShareLinkTripId/ShareLinkRole set,
/// UserId left null). A request is never both; ITripAccessService is the single place that reads
/// this to decide access, so callers never need to branch on which kind of identity this is.
/// </summary>
public interface ICurrentUserService
{
    int? UserId { get; set; }

    int? ShareLinkTripId { get; set; }

    TripRole? ShareLinkRole { get; set; }
}
