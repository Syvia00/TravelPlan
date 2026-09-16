using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportTravelLegDto(
    TransportType TransportType,
    string DepartureLocation,
    string ArrivalLocation,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    int DurationMinutes,
    string? ConfirmationCode
);
