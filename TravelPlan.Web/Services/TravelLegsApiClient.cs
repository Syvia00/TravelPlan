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

    /// <summary>
    /// Raw PNG bytes for the optional static map thumbnail (TravelPlan-Project-Plan-v2.md §2), or
    /// null if unavailable — no Maps key configured, or either location couldn't be geocoded. Not
    /// JSON, so this bypasses ApiClientBase's helpers rather than forcing a byte[] through them.
    /// </summary>
    public async Task<byte[]?> GetMapThumbnailAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/travel-legs/{id}/map", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
