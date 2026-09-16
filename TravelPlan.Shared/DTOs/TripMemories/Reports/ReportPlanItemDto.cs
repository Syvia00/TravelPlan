namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportPlanItemDto(
    DateOnly? Date,
    TimeOnly? Time,
    string Title,
    string? Notes
);
