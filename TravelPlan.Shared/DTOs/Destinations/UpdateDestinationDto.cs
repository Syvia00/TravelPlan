namespace TravelPlan.Shared.DTOs.Destinations;

public record UpdateDestinationDto(
    string Name,
    string CountryCode,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    string? Notes
);
