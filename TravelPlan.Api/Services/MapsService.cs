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
/// Confirmed live against a real AzureMaps:SubscriptionKey (2026-10-05) — including one genuine bug
/// this caught: the static image call originally used `bbox` for auto-framing, which Azure Maps
/// rejects outright in combination with `width`/`height` ("Bbox may not be used in conjunction with
/// center and/or width and/or height") — confirmed via direct curl against the real API, not from
/// documentation. Fixed by computing `center`+`zoom` ourselves (ComputeFitZoom, standard Web Mercator
/// fit-bounds math) instead of delegating the framing to `bbox`.
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

        const int width = 512;
        const int height = 512;

        var centerLon = (from.Longitude + to.Longitude) / 2;
        var centerLat = (from.Latitude + to.Latitude) / 2;
        var zoom = ComputeFitZoom(from, to, width, height);

        var pins = $"default||{Coord(from.Longitude)} {Coord(from.Latitude)}|{Coord(to.Longitude)} {Coord(to.Latitude)}";
        var url = $"map/static?api-version={StaticImageApiVersion}&subscription-key={key}" +
                   $"&layer=basic&style=main&width={width}&height={height}" +
                   $"&center={Coord(centerLon)},{Coord(centerLat)}&zoom={zoom}" +
                   $"&pins={Uri.EscapeDataString(pins)}";

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

    // Standard Web Mercator "fit bounds to viewport" zoom calculation (the same math underlying
    // Leaflet's fitBounds/Mapbox's cameraForBounds) — Azure Maps' static image API does not do this
    // itself: `bbox` (its only other framing option) can't be combined with `width`/`height` at all
    // (confirmed directly against the API, see this class's doc comment), so there's no way to ask
    // it for "pick whatever zoom fits this box" at a size we control. 256px tiles, doubling per zoom
    // level, is Azure Maps' own tiling convention (matches Bing/Google) — confirmed by reproducing
    // the exact zoom boundaries `bbox` mode reported (e.g. zoom 6 capping longitude span at
    // 21.97265625°, exactly 64× zoom 12's reported 0.3433227539° cap) before this fix replaced bbox.
    internal static int ComputeFitZoom((double Latitude, double Longitude) a, (double Latitude, double Longitude) b, int widthPx, int heightPx)
    {
        const double tileSize = 256;
        const int maxZoom = 15;
        const double paddingFactor = 0.8; // leaves ~10% margin on each side instead of pins flush against the edge.

        var lonSpan = Math.Max(Math.Abs(a.Longitude - b.Longitude), 0.0001);
        var zoomForLon = Math.Log2(widthPx * paddingFactor * 360 / (lonSpan * tileSize));

        var mercatorY1 = MercatorY(a.Latitude);
        var mercatorY2 = MercatorY(b.Latitude);
        var latSpanMercator = Math.Max(Math.Abs(mercatorY1 - mercatorY2), 0.0001);
        var zoomForLat = Math.Log2(heightPx * paddingFactor * 2 * Math.PI / (latSpanMercator * tileSize));

        var zoom = (int)Math.Floor(Math.Min(zoomForLon, zoomForLat));
        return Math.Clamp(zoom, 1, maxZoom);
    }

    private static double MercatorY(double latitudeDegrees)
    {
        var latRadians = latitudeDegrees * Math.PI / 180;
        return Math.Log(Math.Tan(Math.PI / 4 + latRadians / 2));
    }

    private static string Coord(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
}
