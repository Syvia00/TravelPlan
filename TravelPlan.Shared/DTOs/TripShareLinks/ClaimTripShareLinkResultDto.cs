using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripShareLinks;

/// <summary>Mirrors TripAccessService's TripAccessInfo shape — Role is null when IsOwner.</summary>
public record ClaimTripShareLinkResultDto(int TripId, bool IsOwner, TripRole? Role);
