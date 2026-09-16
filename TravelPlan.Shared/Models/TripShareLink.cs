using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class TripShareLink
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public string Token { get; set; } = string.Empty;

    public TripRole Role { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Trip? Trip { get; set; }
}
