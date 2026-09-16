using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class Trip
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TripStatus Status { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? Currency { get; set; }

    public decimal? TotalBudget { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }

    public ICollection<Destination> Destinations { get; set; } = new List<Destination>();

    public ICollection<PlanItem> PlanItems { get; set; } = new List<PlanItem>();

    public ICollection<Accommodation> Accommodations { get; set; } = new List<Accommodation>();

    public ICollection<TravelLeg> TravelLegs { get; set; } = new List<TravelLeg>();

    public ICollection<BudgetItem> BudgetItems { get; set; } = new List<BudgetItem>();

    public ICollection<TripMemory> TripMemories { get; set; } = new List<TripMemory>();

    public ICollection<TripCollaborator> Collaborators { get; set; } = new List<TripCollaborator>();

    public ICollection<TripShareLink> ShareLinks { get; set; } = new List<TripShareLink>();
}
