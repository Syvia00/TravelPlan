using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.BudgetItems;

public record BudgetItemDto(
    int Id,
    int TripId,
    BudgetCategory Category,
    string Description,
    decimal Amount,
    string Currency,
    DateOnly Date,
    bool IsPaid
);
