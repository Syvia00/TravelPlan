namespace TravelPlan.Shared.DTOs.PlanItems;

public record UpdatePlanItemDto(
    int? DestinationId,
    DateOnly? Date,
    TimeOnly? Time,
    string Title,
    string? Notes,
    int SortOrder,
    string? SourceUrl,
    bool IsDone
);
