using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services;

/// <summary>Scoped per-request; set once by UserSyncMiddleware.</summary>
public class CurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }

    public int? ShareLinkTripId { get; set; }

    public TripRole? ShareLinkRole { get; set; }
}
