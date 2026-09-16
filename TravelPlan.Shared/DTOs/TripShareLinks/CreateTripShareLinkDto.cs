using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripShareLinks;

/// <summary>Token is generated server-side; not supplied by the client.</summary>
public record CreateTripShareLinkDto(
    int TripId,
    TripRole Role,
    DateTime? ExpiresAt
);
