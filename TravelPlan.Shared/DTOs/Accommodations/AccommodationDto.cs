namespace TravelPlan.Shared.DTOs.Accommodations;

public record AccommodationDto(
    int Id,
    int TripId,
    int? DestinationId,
    string Name,
    string? Address,
    DateTime CheckIn,
    DateTime CheckOut,
    string? ConfirmationCode,
    string? Notes
);
