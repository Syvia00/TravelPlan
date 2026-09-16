using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

/// <summary>
/// Serialized as TripMemories.ReportData (nvarchar max JSON). See erd.md's "ReportData JSON structure" section.
/// Reflection is user-written text, MemorySummary only — always null/empty for an Itinerary report.
/// </summary>
public record TripReportDto(
    TripMemoryReportType ReportType,
    DateTime GeneratedAt,
    ReportTripHeaderDto Trip,
    IReadOnlyList<ReportDestinationDto> Destinations,
    IReadOnlyList<ReportPlanItemDto> PlanItems,
    ReportBudgetSummaryDto BudgetSummary,
    IReadOnlyList<ReportAccommodationDto> Accommodations,
    IReadOnlyList<ReportTravelLegDto> TravelLegs,
    IReadOnlyList<string> Companions,
    string? Reflection
);
