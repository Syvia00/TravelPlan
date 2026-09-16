using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripCollaborators;

public record TripCollaboratorDto(
    int Id,
    int TripId,
    int UserId,
    string UserDisplayName,
    string? UserEmail,
    TripRole Role,
    DateTime InvitedAt,
    DateTime? AcceptedAt
);
