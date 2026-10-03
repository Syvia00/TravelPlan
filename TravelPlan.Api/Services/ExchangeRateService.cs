using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.DTOs.ExchangeRates;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Services;

/// <summary>
/// FX rates, cached — never fetched from a request path. RefreshRatesAsync (called only by
/// ExchangeRateRefreshBackgroundService, on a schedule) is the one place that ever talks to the
/// provider; ConvertAsync (called from ExchangeRatesController, i.e. ultimately by budget screens)
/// only ever reads the ExchangeRates table.
///
/// Frankfurter's `/v1/latest?base=USD` (no `symbols` filter) returns every currency it supports in
/// one call — confirmed live: ~29 currencies, each expressed as "units of that currency per 1 USD".
/// Caching that single USD-anchored table, rather than one row per every possible (from, to) pair,
/// is what makes ConvertAsync able to answer ANY pair from the cache: convert FROM units to USD
/// (divide by rate[FROM]), then USD to TO units (multiply by rate[TO]) — one HTTP call per refresh
/// covers every pair, not O(currencies²).
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    private const string AnchorCurrency = "USD";

    private readonly HttpClient _httpClient;
    private readonly TravelPlanDbContext _context;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(HttpClient httpClient, TravelPlanDbContext context, ILogger<ExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _logger = logger;
    }

    public async Task RefreshRatesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<FrankfurterLatestResponse>(
            $"v1/latest?base={AnchorCurrency}", cancellationToken);

        if (response?.Rates is null or { Count: 0 })
        {
            _logger.LogWarning("Frankfurter returned no rates on refresh (base={Base}).", AnchorCurrency);
            return;
        }

        var now = DateTime.UtcNow;
        var quoteCurrencies = response.Rates.Keys.ToList();

        var existing = await _context.ExchangeRates
            .Where(r => r.BaseCurrency == AnchorCurrency && quoteCurrencies.Contains(r.QuoteCurrency))
            .ToDictionaryAsync(r => r.QuoteCurrency, cancellationToken);

        foreach (var (currency, rate) in response.Rates)
        {
            if (existing.TryGetValue(currency, out var row))
            {
                row.Rate = rate;
                row.FetchedAt = now;
            }
            else
            {
                _context.ExchangeRates.Add(new ExchangeRate
                {
                    BaseCurrency = AnchorCurrency,
                    QuoteCurrency = currency,
                    Rate = rate,
                    FetchedAt = now,
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refreshed {Count} exchange rate(s) (base={Base}).", response.Rates.Count, AnchorCurrency);
    }

    public async Task<CurrencyConversionDto?> ConvertAsync(string fromCurrency, string toCurrency, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount < 0)
        {
            return null;
        }

        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        if (from == to)
        {
            return new CurrencyConversionDto(from, to, amount, amount, 1m, DateTime.UtcNow);
        }

        decimal? rateFrom = null;
        decimal? rateTo = null;
        DateTime? fetchedAt = null;

        if (from != AnchorCurrency || to != AnchorCurrency)
        {
            var needed = new[] { from, to }.Where(c => c != AnchorCurrency).ToList();
            var rows = await _context.ExchangeRates
                .Where(r => r.BaseCurrency == AnchorCurrency && needed.Contains(r.QuoteCurrency))
                .ToDictionaryAsync(r => r.QuoteCurrency, cancellationToken);

            rateFrom = from == AnchorCurrency ? 1m : rows.TryGetValue(from, out var f) ? f.Rate : null;
            rateTo = to == AnchorCurrency ? 1m : rows.TryGetValue(to, out var t) ? t.Rate : null;
            fetchedAt = rows.Values.Select(r => r.FetchedAt).DefaultIfEmpty().Min();
        }

        if (rateFrom is not { } rf || rateTo is not { } rt || rf == 0)
        {
            return null;
        }

        var converted = amount / rf * rt;
        var pairRate = rt / rf;

        return new CurrencyConversionDto(from, to, amount, converted, pairRate, fetchedAt ?? DateTime.UtcNow);
    }

    private sealed class FrankfurterLatestResponse
    {
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
