using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Services;

namespace TravelPlan.Api.Controllers;

/// <summary>
/// Reads only — always from the ExchangeRates cache, never the external provider directly. See
/// IExchangeRateService/ExchangeRateRefreshBackgroundService for how the cache gets populated.
/// </summary>
[ApiController]
[Authorize]
[Route("api/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRates;

    public ExchangeRatesController(IExchangeRateService exchangeRates)
    {
        _exchangeRates = exchangeRates;
    }

    /// <summary>Not trip-scoped data, so no ITripAccessService check — any signed-in user can convert.</summary>
    [HttpGet("convert")]
    public async Task<IActionResult> Convert(
        [FromQuery] string from, [FromQuery] string to, [FromQuery] decimal amount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(from) || from.Trim().Length != 3)
        {
            ModelState.AddModelError(nameof(from), "from must be a 3-letter ISO 4217 currency code.");
        }

        if (string.IsNullOrWhiteSpace(to) || to.Trim().Length != 3)
        {
            ModelState.AddModelError(nameof(to), "to must be a 3-letter ISO 4217 currency code.");
        }

        if (amount < 0)
        {
            ModelState.AddModelError(nameof(amount), "amount must not be negative.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _exchangeRates.ConvertAsync(from, to, amount, cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = $"No cached rate for {from.ToUpperInvariant()}/{to.ToUpperInvariant()} yet." });
        }

        return Ok(result);
    }
}
