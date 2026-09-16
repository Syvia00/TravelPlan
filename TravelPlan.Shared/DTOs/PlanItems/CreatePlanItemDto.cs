namespace TravelPlan.Shared.DTOs.PlanItems;

public record CreatePlanItemDto(
    int TripId,
    int? DestinationId,
    DateOnly? Date,
    TimeOnly? Time,
    string Title,
    string? Notes,
    int SortOrder,
    string? SourceUrl
);
