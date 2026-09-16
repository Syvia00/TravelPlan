using TravelPlan.Shared.DTOs.BudgetItems;

namespace TravelPlan.Web.Services;

public class BudgetItemsApiClient : ApiClientBase
{
    public BudgetItemsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<BudgetItemDto>> ListByTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<BudgetItemDto>>($"api/budget-items?tripId={tripId}", cancellationToken);

    public Task<BudgetItemDto> CreateAsync(CreateBudgetItemDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<BudgetItemDto>("api/budget-items", dto, cancellationToken);

    public Task<BudgetItemDto> UpdateAsync(int id, UpdateBudgetItemDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<BudgetItemDto>($"api/budget-items/{id}", dto, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/budget-items/{id}", cancellationToken);
}
