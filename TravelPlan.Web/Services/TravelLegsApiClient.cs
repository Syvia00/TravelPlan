using TravelPlan.Shared.DTOs.TravelLegs;

namespace TravelPlan.Web.Services;

public class TravelLegsApiClient : ApiClientBase
{
    public TravelLegsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<TravelLegDto>> ListByTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<TravelLegDto>>($"api/travel-legs?tripId={tripId}", cancellationToken);

    public Task<TravelLegDto> CreateAsync(CreateTravelLegDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<TravelLegDto>("api/travel-legs", dto, cancellationToken);

    public Task<TravelLegDto> UpdateAsync(int id, UpdateTravelLegDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<TravelLegDto>($"api/travel-legs/{id}", dto, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/travel-legs/{id}", cancellationToken);
}
