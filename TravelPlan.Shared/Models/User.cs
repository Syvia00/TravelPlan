namespace TravelPlan.Shared.Models;

public class User
{
    public int Id { get; set; }

    /// <summary>Microsoft Entra External ID object ID; nullable for phone-only accounts.</summary>
    public string? ExternalAuthId { get; set; }

    /// <summary>Nullable for phone-only accounts.</summary>
    public string? Email { get; set; }

    /// <summary>Nullable, E.164 format.</summary>
    public string? PhoneNumber { get; set; }

    public DateTime? PhoneVerifiedAt { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();

    public ICollection<Destination> Destinations { get; set; } = new List<Destination>();

    public ICollection<TripMemory> TripMemories { get; set; } = new List<TripMemory>();

    public ICollection<TripCollaborator> TripCollaborations { get; set; } = new List<TripCollaborator>();
}
