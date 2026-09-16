using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.Trips;

public record UpdateTripDto(
    string Title,
    string? Description,
    TripStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Currency,
    decimal? TotalBudget
);
