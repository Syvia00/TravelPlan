using TravelPlan.Shared.DTOs.TripMemories;

namespace TravelPlan.Web.Services;

public class TripMemoriesApiClient : ApiClientBase
{
    public TripMemoriesApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<TripMemoryDto>> ListByTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<TripMemoryDto>>($"api/trip-memories?tripId={tripId}", cancellationToken);

    public Task<TripMemoryDto> CreateAsync(CreateTripMemoryDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<TripMemoryDto>("api/trip-memories", dto, cancellationToken);

    public Task<TripMemoryDto> UpdateReflectionAsync(int id, UpdateTripMemoryReflectionDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<TripMemoryDto>($"api/trip-memories/{id}/reflection", dto, cancellationToken);

    /// <summary>Absolute API URLs — these are plain downloads/inline views, so a raw &lt;a href&gt;
    /// to the API origin is enough; the API sets Content-Disposition itself for PNG/PDF.</summary>
    public Uri GetPngUrl(int id) => new(Http.BaseAddress!, $"api/trip-memories/{id}/png");

    public Uri GetPdfUrl(int id) => new(Http.BaseAddress!, $"api/trip-memories/{id}/pdf");

    public Uri GetHtmlUrl(int id) => new(Http.BaseAddress!, $"api/trip-memories/{id}/html");
}
