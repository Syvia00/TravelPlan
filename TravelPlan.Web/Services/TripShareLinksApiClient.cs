using TravelPlan.Shared.DTOs.TripShareLinks;

namespace TravelPlan.Web.Services;

public class TripShareLinksApiClient : ApiClientBase
{
    public TripShareLinksApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<TripShareLinkDto>> ListAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<TripShareLinkDto>>($"api/trip-share-links?tripId={tripId}", cancellationToken);

    public Task<TripShareLinkDto> CreateAsync(CreateTripShareLinkDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<TripShareLinkDto>("api/trip-share-links", dto, cancellationToken);

    public Task RevokeAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/trip-share-links/{id}", cancellationToken);
}
