namespace TravelPlan.Shared.DTOs.PlanItems;

public record PlanItemDto(
    int Id,
    int TripId,
    int? DestinationId,
    DateOnly? Date,
    TimeOnly? Time,
    string Title,
    string? Notes,
    int SortOrder,
    string? SourceUrl,
    bool IsDone
);
