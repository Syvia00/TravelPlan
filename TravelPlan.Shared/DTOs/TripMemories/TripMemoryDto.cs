using TravelPlan.Shared.DTOs.TripMemories.Reports;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripMemories;

public record TripMemoryDto(
    int Id,
    int TripId,
    int UserId,
    TripMemoryReportType ReportType,
    string Title,
    string? Description,
    TripReportDto ReportData,
    DateTime CreatedAt
);
