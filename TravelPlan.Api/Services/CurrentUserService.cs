namespace TravelPlan.Api.Services;

/// <summary>Scoped per-request; set once by UserSyncMiddleware.</summary>
public class CurrentUserService : ICurrentUserService
{
    public int? UserId { get; set; }
}
