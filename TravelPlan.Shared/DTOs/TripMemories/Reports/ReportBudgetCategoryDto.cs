using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportBudgetCategoryDto(
    BudgetCategory Category,
    decimal Total,
    decimal Percentage
);
