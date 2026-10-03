namespace TravelPlan.Shared.DTOs.ExchangeRates;

/// <summary>Result of converting an amount between two currencies using the cached rate table.</summary>
public record CurrencyConversionDto(
    string FromCurrency,
    string ToCurrency,
    decimal Amount,
    decimal ConvertedAmount,
    decimal Rate,
    DateTime RateFetchedAt
);
