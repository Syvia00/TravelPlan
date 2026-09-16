namespace TravelPlan.Shared.Models;

public class Destination
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2/3.</summary>
    public string CountryCode { get; set; } = string.Empty;

    public DateOnly EntryDate { get; set; }

    public DateOnly? ExitDate { get; set; }

    public string? Notes { get; set; }

    public Trip? Trip { get; set; }

    public User? User { get; set; }

    public ICollection<Accommodation> Accommodations { get; set; } = new List<Accommodation>();

    public ICollection<PlanItem> PlanItems { get; set; } = new List<PlanItem>();
}
