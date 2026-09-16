namespace TravelPlan.Shared.DTOs.Destinations;

public record DestinationDto(
    int Id,
    int TripId,
    string Name,
    string CountryCode,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    string? Notes
);
