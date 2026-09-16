using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class TravelLeg
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public string DepartureLocation { get; set; } = string.Empty;

    public string ArrivalLocation { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }

    public DateTime ArrivalTime { get; set; }

    public TransportType TransportType { get; set; }

    public string? ConfirmationCode { get; set; }

    public string? Notes { get; set; }

    public Trip? Trip { get; set; }
}
