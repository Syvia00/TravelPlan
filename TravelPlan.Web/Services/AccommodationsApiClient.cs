using TravelPlan.Shared.DTOs.Accommodations;

namespace TravelPlan.Web.Services;

public class AccommodationsApiClient : ApiClientBase
{
    public AccommodationsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<AccommodationDto>> ListByTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<AccommodationDto>>($"api/accommodations?tripId={tripId}", cancellationToken);

    public Task<AccommodationDto> CreateAsync(CreateAccommodationDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<AccommodationDto>("api/accommodations", dto, cancellationToken);

    public Task<AccommodationDto> UpdateAsync(int id, UpdateAccommodationDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<AccommodationDto>($"api/accommodations/{id}", dto, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/accommodations/{id}", cancellationToken);
}
