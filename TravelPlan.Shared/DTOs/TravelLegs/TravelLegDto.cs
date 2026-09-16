using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TravelLegs;

public record TravelLegDto(
    int Id,
    int TripId,
    string DepartureLocation,
    string ArrivalLocation,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    TransportType TransportType,
    string? ConfirmationCode,
    string? Notes
);
