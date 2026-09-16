using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class TripCollaborator
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public int UserId { get; set; }

    public TripRole Role { get; set; }

    public DateTime InvitedAt { get; set; }

    /// <summary>Null until the invited user signs up/accepts.</summary>
    public DateTime? AcceptedAt { get; set; }

    public Trip? Trip { get; set; }

    public User? User { get; set; }
}
