namespace TravelPlan.Shared.DTOs.Destinations;

public record CreateDestinationDto(
    int TripId,
    string Name,
    string CountryCode,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    string? Notes
);
