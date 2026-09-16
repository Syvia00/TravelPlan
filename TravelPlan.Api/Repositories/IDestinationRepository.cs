using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

/// <summary>No public CRUD endpoints exist for Destinations yet — this exists purely for
/// TripReportService to read a trip's destinations. Extend when Destinations CRUD ships.</summary>
public interface IDestinationRepository : IRepository<Destination>
{
    Task<List<Destination>> ListByTripIdAsync(int tripId, CancellationToken cancellationToken = default);
}
