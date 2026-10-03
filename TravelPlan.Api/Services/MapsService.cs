using System.Globalization;
using System.Text.Json;

namespace TravelPlan.Api.Services;

/// <summary>
/// Thin server-side wrapper around Azure Maps' REST APIs (Geocode, Get Map Static Image) —
/// AzureMaps:SubscriptionKey never reaches a client; every call this makes is server-to-server, and
/// GetTravelLegMapThumbnailAsync returns raw image bytes for the API to stream back itself (see
/// TravelLegsController), not a URL the browser would have to call directly with the key attached.
///
/// Stateless and uncached deliberately — unlike ExchangeRateService, erd.md defines no cache table
/// for this, and map thumbnails are requested far less often (per transport leg, on demand) than FX
/// conversions (every budget screen render), so there's no equivalent pressure to avoid a live call.
///
/// Note: built against Azure Maps' documented Geocode and Get Map Static Image REST shapes: this
/// has NOT been exercised against a real subscription key (none was available while building it) —
/// unlike the Frankfurter integration, which was verified live. Worth a real smoke test with a live
/// AzureMaps:SubscriptionKey before relying on it.
/// </summary>
public class MapsService : IMapsService
{
    private const string GeocodeApiVersion = "2023-06-01";
    private const string StaticImageApiVersion = "2024-04-01";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MapsService> _logger;

    public MapsService(HttpClient httpClient, IConfiguration configuration, ILogger<MapsService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(string query, CancellationToken cancellationToken = default)
    {
        var key = GetSubscriptionKeyOrNull();
        if (key is null || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var url = $"geocode?api-version={GeocodeApiVersion}&query={Uri.EscapeDataString(query)}&subscription-key={key}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azure Maps geocode failed for {Query}: {StatusCode}", query, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var features = document.RootElement.GetProperty("features");
            if (features.GetArrayLength() == 0)
            {
                return null;
            }

            var coordinates = features[0].GetProperty("geometry").GetProperty("coordinates");
            var longitude = coordinates[0].GetDouble();
            var latitude = coordinates[1].GetDouble();
            return (latitude, longitude);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Azure Maps geocode request failed for {Query}.", query);
            return null;
        }
    }

    public async Task<byte[]?> GetTravelLegMapThumbnailAsync(string departureLocation, string arrivalLocation, CancellationToken cancellationToken = default)
    {
        var key = GetSubscriptionKeyOrNull();
        if (key is null)
        {
            return null;
        }

        var departure = await GeocodeAsync(departureLocation, cancellationToken);
        var arrival = await GeocodeAsync(arrivalLocation, cancellationToken);
        if (departure is not { } from || arrival is not { } to)
        {
            return null;
        }

        var bbox = BuildBoundingBox(from, to);
        var pins = $"default||{Coord(from.Longitude)} {Coord(from.Latitude)}|{Coord(to.Longitude)} {Coord(to.Latitude)}";
        var url = $"map/static?api-version={StaticImageApiVersion}&subscription-key={key}" +
                   $"&layer=basic&style=main&width=512&height=512" +
                   $"&bbox={bbox}&pins={Uri.EscapeDataString(pins)}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Azure Maps static image failed for {Departure} -> {Arrival}: {StatusCode}",
                    departureLocation, arrivalLocation, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Azure Maps static image request failed for {Departure} -> {Arrival}.", departureLocation, arrivalLocation);
            return null;
        }
    }

    private string? GetSubscriptionKeyOrNull()
    {
        var key = _configuration["AzureMaps:SubscriptionKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("AzureMaps:SubscriptionKey is not configured — maps features are unavailable.");
            return null;
        }

        return key;
    }

    // Pads a fixed-degree margin around both points rather than a percentage of their span, so two
    // pins right next to each other (a short leg) still render a sensibly zoomed-out thumbnail
    // instead of a near-zero-area bbox Azure Maps would reject or render unusably close-up.
    private static string BuildBoundingBox((double Latitude, double Longitude) a, (double Latitude, double Longitude) b)
    {
        const double minPadding = 0.5;

        var minLon = Math.Min(a.Longitude, b.Longitude);
        var maxLon = Math.Max(a.Longitude, b.Longitude);
        var minLat = Math.Min(a.Latitude, b.Latitude);
        var maxLat = Math.Max(a.Latitude, b.Latitude);

        var lonPadding = Math.Max(minPadding, (maxLon - minLon) * 0.15);
        var latPadding = Math.Max(minPadding, (maxLat - minLat) * 0.15);

        return string.Join(',',
            Coord(minLon - lonPadding), Coord(minLat - latPadding),
            Coord(maxLon + lonPadding), Coord(maxLat + latPadding));
    }

    private static string Coord(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
}
