namespace TravelPlan.Shared.DTOs.ExchangeRates;

/// <summary>Read-only — populated by the background FX refresh job, not created via client requests.</summary>
public record ExchangeRateDto(
    int Id,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    DateTime FetchedAt
);
