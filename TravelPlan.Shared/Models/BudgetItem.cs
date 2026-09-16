using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class BudgetItem
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public BudgetCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateOnly Date { get; set; }

    public bool IsPaid { get; set; }

    public Trip? Trip { get; set; }
}
