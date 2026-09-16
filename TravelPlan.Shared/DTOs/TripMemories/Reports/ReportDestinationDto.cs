namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportDestinationDto(
    string Name,
    string CountryCode,
    DateOnly EntryDate,
    DateOnly? ExitDate,
    int? Nights
);
