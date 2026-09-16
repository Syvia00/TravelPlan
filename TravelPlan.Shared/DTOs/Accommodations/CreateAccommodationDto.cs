namespace TravelPlan.Shared.DTOs.Accommodations;

public record CreateAccommodationDto(
    int TripId,
    int? DestinationId,
    string Name,
    string? Address,
    DateTime CheckIn,
    DateTime CheckOut,
    string? ConfirmationCode,
    string? Notes
);
