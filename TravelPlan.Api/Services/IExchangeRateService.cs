using TravelPlan.Shared.DTOs.ExchangeRates;

namespace TravelPlan.Api.Services;

public interface IExchangeRateService
{
    /// <summary>
    /// Calls the FX provider (Frankfurter) once and upserts the result into the ExchangeRates
    /// cache, one row per currency the provider returns, anchored to USD. Used only by
    /// ExchangeRateRefreshBackgroundService — never called from a request path.
    /// </summary>
    Task RefreshRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount between two ISO 4217 currency codes using only the cached rate table —
    /// never calls the external API. Returns null if either currency isn't in the cache (not yet
    /// refreshed, or not one Frankfurter supports) or if amount is negative.
    /// </summary>
    Task<CurrencyConversionDto?> ConvertAsync(string fromCurrency, string toCurrency, decimal amount, CancellationToken cancellationToken = default);
}
