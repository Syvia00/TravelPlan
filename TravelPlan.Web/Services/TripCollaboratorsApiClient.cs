using TravelPlan.Shared.DTOs.TripCollaborators;

namespace TravelPlan.Web.Services;

public class TripCollaboratorsApiClient : ApiClientBase
{
    public TripCollaboratorsApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<TripCollaboratorDto>> ListAsync(int tripId, CancellationToken cancellationToken = default) =>
        GetAsync<List<TripCollaboratorDto>>($"api/trip-collaborators?tripId={tripId}", cancellationToken);

    public Task<TripCollaboratorDto> InviteAsync(CreateTripCollaboratorDto dto, CancellationToken cancellationToken = default) =>
        PostAsync<TripCollaboratorDto>("api/trip-collaborators", dto, cancellationToken);

    public Task<TripCollaboratorDto> UpdateRoleAsync(int id, UpdateTripCollaboratorDto dto, CancellationToken cancellationToken = default) =>
        PutAsync<TripCollaboratorDto>($"api/trip-collaborators/{id}", dto, cancellationToken);

    public Task RevokeAsync(int id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/trip-collaborators/{id}", cancellationToken);
}
