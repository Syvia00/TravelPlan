namespace TravelPlan.Shared.DTOs.Trips;

public record CreateTripDto(
    string Title,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Currency,
    decimal? TotalBudget
);
