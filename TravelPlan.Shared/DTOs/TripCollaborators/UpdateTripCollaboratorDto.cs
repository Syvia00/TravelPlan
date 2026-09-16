using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripCollaborators;

public record UpdateTripCollaboratorDto(
    TripRole Role
);
