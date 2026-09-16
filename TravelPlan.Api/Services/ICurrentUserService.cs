namespace TravelPlan.Api.Services;

/// <summary>Resolved by UserSyncMiddleware from the authenticated request's ExternalAuthId claim.</summary>
public interface ICurrentUserService
{
    int? UserId { get; set; }
}
