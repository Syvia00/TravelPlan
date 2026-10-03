using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TravelPlan.Api.Services;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises MapsService's own logic — subscription-key gating, geocode response parsing, and the
/// "both ends must geocode or no thumbnail" rule — against a stubbed HttpMessageHandler rather than
/// real Azure Maps (no subscription key was available while building this; see MapsService's doc
/// comment). These tests confirm MapsService's own code behaves as designed; they do not confirm
/// Azure Maps' real API matches the request shapes MapsService sends.
/// </summary>
public class MapsServiceTests
{
    private const string GeocodeFoundResponse =
        """{"type":"FeatureCollection","features":[{"type":"Feature","geometry":{"type":"Point","coordinates":[-122.3321,47.6062]},"properties":{}}]}""";

    private const string GeocodeNotFoundResponse =
        """{"type":"FeatureCollection","features":[]}""";

    private static IConfiguration ConfigWithKey(string? key) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(key is null
                ? []
                : new Dictionary<string, string?> { ["AzureMaps:SubscriptionKey"] = key })
            .Build();

    private static MapsService Service(FakeHttpMessageHandler handler, string? subscriptionKey = "test-key")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://atlas.microsoft.com/") };
        return new MapsService(httpClient, ConfigWithKey(subscriptionKey), NullLogger<MapsService>.Instance);
    }

    [Fact]
    public async Task GeocodeAsync_NoSubscriptionKeyConfigured_ReturnsNullWithoutCallingTheApi()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = Service(handler, subscriptionKey: null);

        var result = await service.GeocodeAsync("Seattle, WA");

        Assert.Null(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetTravelLegMapThumbnailAsync_NoSubscriptionKeyConfigured_ReturnsNullWithoutCallingTheApi()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("should not be called"));
        var service = Service(handler, subscriptionKey: null);

        var result = await service.GetTravelLegMapThumbnailAsync("Seattle, WA", "Dallas, TX");

        Assert.Null(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GeocodeAsync_ParsesLongitudeLatitudeFromGeoJsonCorrectly()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GeocodeFoundResponse, Encoding.UTF8, "application/json"),
        });
        var service = Service(handler);

        var result = await service.GeocodeAsync("Seattle, WA");

        Assert.NotNull(result);
        // GeoJSON orders coordinates [lon, lat] — confirms MapsService doesn't swap them.
        Assert.Equal(47.6062, result.Value.Latitude, precision: 4);
        Assert.Equal(-122.3321, result.Value.Longitude, precision: 4);
    }

    [Fact]
    public async Task GeocodeAsync_NoFeaturesInResponse_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GeocodeNotFoundResponse, Encoding.UTF8, "application/json"),
        });
        var service = Service(handler);

        var result = await service.GeocodeAsync("Nowhere, Nowhere");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTravelLegMapThumbnailAsync_BothEndsGeocode_ReturnsStaticImageBytes()
    {
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path.StartsWith("/map/static")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(imageBytes) }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GeocodeFoundResponse, Encoding.UTF8, "application/json"),
                };
        });
        var service = Service(handler);

        var result = await service.GetTravelLegMapThumbnailAsync("Seattle, WA", "Dallas, TX");

        Assert.Equal(imageBytes, result);
        // 2 geocode calls (departure, arrival) + 1 static image call.
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GetTravelLegMapThumbnailAsync_OneEndFailsToGeocode_ReturnsNullAndNeverRequestsTheImage()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            var content = query.Contains("Nowhere") ? GeocodeNotFoundResponse : GeocodeFoundResponse;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
        });
        var service = Service(handler);

        var result = await service.GetTravelLegMapThumbnailAsync("Seattle, WA", "Nowhere, Nowhere");

        Assert.Null(result);
        // Both geocode attempts happen (departure then arrival) — but no static image request.
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/map/static"));
    }
}
