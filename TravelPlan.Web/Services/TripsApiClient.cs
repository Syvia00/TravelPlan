using TravelPlan.Shared.DTOs.Trips;

namespace TravelPlan.Web.Services;

public class TripsApiClient : ApiClientBase
{
    public TripsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<TripDto>> ListAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<TripDto>>("api/trips", cancellationToken);

    public Task<TripDto?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<TripDto>($"api/trips/{id}", cancellationToken);

    public Task<TripDto> CreateAsync(CreateTripDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<TripDto>("api/trips", dto, cancellationToken);

    public Task<TripDto> UpdateAsync(int id, UpdateTripDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<TripDto>($"api/trips/{id}", dto, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/trips/{id}", cancellationToken);

    /// <summary>Marks the trip Completed and triggers its MemorySummary report generation.</summary>
    public Task<TripDto> CompleteAsync(int id, CancellationToken cancellationToken = default) =>
        PostAsync<TripDto>($"api/trips/{id}/complete", new { }, cancellationToken);
}
