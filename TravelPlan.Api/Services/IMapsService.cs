namespace TravelPlan.Api.Services;

public interface IMapsService
{
    /// <summary>
    /// Resolves a free-text location (e.g. a TravelLeg's DepartureLocation, or a Destination's
    /// Name) to coordinates via Azure Maps. Returns null if AzureMaps:SubscriptionKey isn't
    /// configured (optional locally — see README) or the location couldn't be geocoded.
    /// </summary>
    Task<(double Latitude, double Longitude)?> GeocodeAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Geocodes both locations and renders a static map image with a pin at each, framed to fit
    /// both — for TravelPlan-Project-Plan-v2.md §2's "optional static map thumbnail per transport
    /// leg". Returns null (not an exception) if the subscription key is unconfigured or either
    /// location can't be geocoded, so a leg missing a thumbnail never breaks the rest of the page.
    /// </summary>
    Task<byte[]?> GetTravelLegMapThumbnailAsync(string departureLocation, string arrivalLocation, CancellationToken cancellationToken = default);
}
