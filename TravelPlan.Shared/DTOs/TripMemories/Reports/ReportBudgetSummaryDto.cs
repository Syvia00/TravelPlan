namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportBudgetSummaryDto(
    decimal GrandTotal,
    string Currency,
    IReadOnlyList<ReportBudgetCategoryDto> Categories
);
