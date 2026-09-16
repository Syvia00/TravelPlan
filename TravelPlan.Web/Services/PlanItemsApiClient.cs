using TravelPlan.Shared.DTOs.PlanItems;

namespace TravelPlan.Web.Services;

public class PlanItemsApiClient : ApiClientBase
{
    public PlanItemsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<PlanItemDto>> ListByTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<PlanItemDto>>($"api/plan-items?tripId={tripId}", cancellationToken);

    public Task<PlanItemDto> CreateAsync(CreatePlanItemDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<PlanItemDto>("api/plan-items", dto, cancellationToken);

    public Task<PlanItemDto> UpdateAsync(int id, UpdatePlanItemDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<PlanItemDto>($"api/plan-items/{id}", dto, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/plan-items/{id}", cancellationToken);
}
