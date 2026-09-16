using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.DTOs.TravelLegs;

public record CreateTravelLegDto(
    int TripId,
    string DepartureLocation,
    string ArrivalLocation,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    TransportType TransportType,
    string? ConfirmationCode,
    string? Notes
);
