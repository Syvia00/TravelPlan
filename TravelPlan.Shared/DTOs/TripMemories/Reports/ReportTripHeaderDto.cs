namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportTripHeaderDto(
    int Id,
    string Title,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    int DurationDays
);
