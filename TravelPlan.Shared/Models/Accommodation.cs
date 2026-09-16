namespace TravelPlan.Shared.Models;

public class Accommodation
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public int? DestinationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public string? ConfirmationCode { get; set; }

    public string? Notes { get; set; }

    public Trip? Trip { get; set; }

    public Destination? Destination { get; set; }
}
