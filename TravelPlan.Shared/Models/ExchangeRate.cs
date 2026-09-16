namespace TravelPlan.Shared.Models;

/// <summary>Standalone FX cache — not tied to a trip or user.</summary>
public class ExchangeRate
{
    public int Id { get; set; }

    public string BaseCurrency { get; set; } = string.Empty;

    public string QuoteCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime FetchedAt { get; set; }
}
