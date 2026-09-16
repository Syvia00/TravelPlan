using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripMemories;

/// <summary>Requests generation of a report for a trip; ReportData is built server-side.</summary>
public record CreateTripMemoryDto(
    int TripId,
    TripMemoryReportType ReportType,
    string? Title,
    string? Description
);
