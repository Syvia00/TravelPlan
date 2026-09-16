using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripShareLinks;

public record TripShareLinkDto(
    int Id,
    int TripId,
    string Token,
    TripRole Role,
    DateTime? ExpiresAt,
    DateTime CreatedAt
);
