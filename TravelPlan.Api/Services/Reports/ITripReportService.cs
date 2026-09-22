using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services.Reports;

public interface ITripReportService
{
    /// <summary>
    /// Builds report data for a trip (access-checked via ITripAccessService, requires Editor) and
    /// persists it as a new TripMemory row. Returns null if the trip doesn't exist or the current
    /// request lacks Editor access. Throws InvalidOperationException for ReportType.MemorySummary
    /// when the trip isn't Completed yet — per erd.md, a memory summary is only ever generated on
    /// trip completion.
    /// </summary>
    Task<TripMemory?> GenerateReportAsync(
        int tripId,
        TripMemoryReportType reportType,
        string? title,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds report data for an already-resolved trip and persists it — no ownership check,
    /// no trip-status guard. For internal use by ITripCompletionService (manual completion and
    /// the auto-completion sweep), which already knows the trip is being completed.
    /// </summary>
    Task<TripMemory> GenerateReportForTripAsync(
        Trip trip,
        TripMemoryReportType reportType,
        string? title,
        string? description,
        CancellationToken cancellationToken = default);
}
