using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TravelLegs;

public record UpdateTravelLegDto(
    string DepartureLocation,
    string ArrivalLocation,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    TransportType TransportType,
    string? ConfirmationCode,
    string? Notes
);
