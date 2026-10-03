using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelPlan.Api.Data;
using TravelPlan.Api.Services;
using TravelPlan.Shared.Models;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises ExchangeRateService against a real SQLite database (RefreshRatesAsync's upsert logic
/// depends on the unique (BaseCurrency, QuoteCurrency) index actually being enforced — EF's
/// InMemory provider wouldn't exercise that) and a stubbed HttpMessageHandler standing in for
/// Frankfurter, using the exact response shape confirmed live against the real API
/// (https://api.frankfurter.dev/v1/latest?base=USD) while building this.
/// </summary>
public class ExchangeRateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;

    public ExchangeRateServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TravelPlanSqliteDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TravelPlanSqliteDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ExchangeRateService Service(TravelPlanDbContext context, string frankfurterJson)
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(frankfurterJson, Encoding.UTF8, "application/json"),
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
        return new ExchangeRateService(httpClient, context, NullLogger<ExchangeRateService>.Instance);
    }

    private const string TwoCurrencyResponse =
        """{"amount":1.0,"base":"USD","date":"2026-10-02","rates":{"EUR":0.89087,"JPY":157.67}}""";

    [Fact]
    public async Task RefreshRatesAsync_InsertsOneRowPerCurrency_AnchoredToUsd()
    {
        var service = Service(_context, TwoCurrencyResponse);

        await service.RefreshRatesAsync();

        var rows = await _context.ExchangeRates.OrderBy(r => r.QuoteCurrency).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("USD", r.BaseCurrency));
        Assert.Equal("EUR", rows[0].QuoteCurrency);
        Assert.Equal(0.89087m, rows[0].Rate);
        Assert.Equal("JPY", rows[1].QuoteCurrency);
        Assert.Equal(157.67m, rows[1].Rate);
    }

    [Fact]
    public async Task RefreshRatesAsync_SecondRefresh_UpdatesExistingRowsInsteadOfDuplicating()
    {
        var service = Service(_context, TwoCurrencyResponse);
        await service.RefreshRatesAsync();

        const string updated = """{"amount":1.0,"base":"USD","date":"2026-10-03","rates":{"EUR":0.90000,"JPY":158.00}}""";
        var secondService = Service(_context, updated);
        await secondService.RefreshRatesAsync();

        var rows = await _context.ExchangeRates.ToListAsync();
        Assert.Equal(2, rows.Count); // still 2, not 4 — refresh upserts, doesn't accumulate history.
        Assert.Equal(0.90000m, rows.Single(r => r.QuoteCurrency == "EUR").Rate);
    }

    [Fact]
    public async Task ConvertAsync_SameCurrency_ReturnsAmountUnchangedWithoutTouchingTheCache()
    {
        var service = Service(_context, TwoCurrencyResponse); // never refreshed — cache is empty.

        var result = await service.ConvertAsync("usd", "USD", 42m);

        Assert.NotNull(result);
        Assert.Equal(42m, result.ConvertedAmount);
        Assert.Equal(1m, result.Rate);
    }

    [Fact]
    public async Task ConvertAsync_UsdToQuoteCurrency_UsesCachedRateDirectly()
    {
        var service = Service(_context, TwoCurrencyResponse);
        await service.RefreshRatesAsync();

        var result = await service.ConvertAsync("USD", "EUR", 100m);

        Assert.NotNull(result);
        Assert.Equal(89.087m, result.ConvertedAmount);
    }

    [Fact]
    public async Task ConvertAsync_BetweenTwoNonUsdCurrencies_ComputesCrossRateThroughUsd()
    {
        var service = Service(_context, TwoCurrencyResponse);
        await service.RefreshRatesAsync();

        // 100 EUR -> USD -> JPY: 100 / 0.89087 * 157.67
        var result = await service.ConvertAsync("EUR", "JPY", 100m);

        Assert.NotNull(result);
        var expected = 100m / 0.89087m * 157.67m;
        Assert.Equal(Math.Round(expected, 6), Math.Round(result.ConvertedAmount, 6));
    }

    [Fact]
    public async Task ConvertAsync_CurrencyNotInCache_ReturnsNull()
    {
        var service = Service(_context, TwoCurrencyResponse);
        await service.RefreshRatesAsync();

        var result = await service.ConvertAsync("USD", "XYZ", 100m);

        Assert.Null(result);
    }

    [Fact]
    public async Task ConvertAsync_NegativeAmount_ReturnsNull()
    {
        var service = Service(_context, TwoCurrencyResponse);
        await service.RefreshRatesAsync();

        var result = await service.ConvertAsync("USD", "EUR", -1m);

        Assert.Null(result);
    }

    [Fact]
    public async Task ConvertAsync_NeverCallsTheExternalApi()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TwoCurrencyResponse, Encoding.UTF8, "application/json"),
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
        var service = new ExchangeRateService(httpClient, _context, NullLogger<ExchangeRateService>.Instance);
        await service.RefreshRatesAsync();
        Assert.Equal(1, callCount); // the one refresh call.

        await service.ConvertAsync("USD", "EUR", 10m);
        await service.ConvertAsync("EUR", "JPY", 10m);
        await service.ConvertAsync("USD", "EUR", 10m);

        Assert.Equal(1, callCount); // still 1 — ConvertAsync never hit the handler.
    }
}
