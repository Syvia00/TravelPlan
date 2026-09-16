using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripCollaborators;

/// <summary>Invites a collaborator by email; pending (no matching UserId) until they sign up.</summary>
public record CreateTripCollaboratorDto(
    int TripId,
    string Email,
    TripRole Role
);
