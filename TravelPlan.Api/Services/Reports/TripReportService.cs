using System.Text.Json;
using TravelPlan.Api.Repositories;
using TravelPlan.Shared.DTOs.TripMemories.Reports;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Services.Reports;

public class TripReportService : ITripReportService
{
    private readonly ITripRepository _trips;
    private readonly ITripAccessService _tripAccess;
    private readonly IDestinationRepository _destinations;
    private readonly IPlanItemRepository _planItems;
    private readonly IAccommodationRepository _accommodations;
    private readonly ITravelLegRepository _travelLegs;
    private readonly IBudgetItemRepository _budgetItems;
    private readonly ITripMemoryRepository _tripMemories;

    public TripReportService(
        ITripRepository trips,
        ITripAccessService tripAccess,
        IDestinationRepository destinations,
        IPlanItemRepository planItems,
        IAccommodationRepository accommodations,
        ITravelLegRepository travelLegs,
        IBudgetItemRepository budgetItems,
        ITripMemoryRepository tripMemories)
    {
        _trips = trips;
        _tripAccess = tripAccess;
        _destinations = destinations;
        _planItems = planItems;
        _accommodations = accommodations;
        _travelLegs = travelLegs;
        _budgetItems = budgetItems;
        _tripMemories = tripMemories;
    }

    public async Task<TripMemory?> GenerateReportAsync(
        int tripId,
        TripMemoryReportType reportType,
        string? title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Editor, cancellationToken))
        {
            return null;
        }

        var trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        if (reportType == TripMemoryReportType.MemorySummary && trip.Status != TripStatus.Completed)
        {
            throw new InvalidOperationException(
                "MemorySummary reports are only generated once a trip is marked Completed — use POST /api/trips/{id}/complete.");
        }

        return await GenerateReportForTripAsync(trip, reportType, title, description, cancellationToken);
    }

    public async Task<TripMemory> GenerateReportForTripAsync(
        Trip trip,
        TripMemoryReportType reportType,
        string? title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var reportData = await BuildReportDataAsync(trip, reportType, cancellationToken);
        var json = JsonSerializer.Serialize(reportData, ReportJson.Options);

        var defaultTitle = reportType == TripMemoryReportType.Itinerary
            ? $"{trip.Title} — Itinerary"
            : $"{trip.Title} — Memory Summary";

        var memory = new TripMemory
        {
            TripId = trip.Id,
            UserId = trip.UserId,
            ReportType = reportType,
            Title = string.IsNullOrWhiteSpace(title) ? defaultTitle : title,
            Description = description,
            ReportData = json,
            CreatedAt = DateTime.UtcNow,
        };

        await _tripMemories.AddAsync(memory, cancellationToken);
        await _tripMemories.SaveChangesAsync(cancellationToken);

        return memory;
    }

    /// <summary>
    /// Builds the shared report payload — identical underlying trip data for both report
    /// types. Itinerary and MemorySummary only differ in ReportType (which the renderers use
    /// to choose a chronological vs. poster-style layout) and in Reflection, which starts null
    /// for both and is only ever filled in afterward, on a MemorySummary, via
    /// PUT /api/trip-memories/{id}/reflection.
    /// </summary>
    private async Task<TripReportDto> BuildReportDataAsync(
        Trip trip, TripMemoryReportType reportType, CancellationToken cancellationToken)
    {
        var destinations = await _destinations.ListByTripIdAsync(trip.Id, cancellationToken);
        var planItems = await _planItems.ListByTripIdAsync(trip.Id, cancellationToken);
        var accommodations = await _accommodations.ListByTripIdAsync(trip.Id, cancellationToken);
        var travelLegs = await _travelLegs.ListByTripIdAsync(trip.Id, cancellationToken);
        var budgetItems = await _budgetItems.ListByTripIdAsync(trip.Id, cancellationToken);

        var tripHeader = new ReportTripHeaderDto(
            trip.Id,
            trip.Title,
            trip.Description,
            trip.StartDate,
            trip.EndDate,
            trip.EndDate.DayNumber - trip.StartDate.DayNumber + 1);

        var reportDestinations = destinations
            .Select(d => new ReportDestinationDto(
                d.Name,
                d.CountryCode,
                d.EntryDate,
                d.ExitDate,
                d.ExitDate is { } exit ? exit.DayNumber - d.EntryDate.DayNumber : null))
            .ToList();

        // Chronological itinerary — the trip-level backlog (Date == null) has no place on a
        // timeline, so it's excluded here even though it's still visible/editable on Home.
        var reportPlanItems = planItems
            .Where(p => p.Date is not null)
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Time ?? TimeOnly.MinValue)
            .Select(p => new ReportPlanItemDto(p.Date, p.Time, p.Title, p.Notes))
            .ToList();

        var reportAccommodations = accommodations
            .OrderBy(a => a.CheckIn)
            .Select(a => new ReportAccommodationDto(
                a.Name,
                a.Address,
                a.CheckIn,
                a.CheckOut,
                Math.Max(0, (a.CheckOut.Date - a.CheckIn.Date).Days),
                a.ConfirmationCode))
            .ToList();

        var reportTravelLegs = travelLegs
            .OrderBy(l => l.DepartureTime)
            .Select(l => new ReportTravelLegDto(
                l.TransportType,
                l.DepartureLocation,
                l.ArrivalLocation,
                l.DepartureTime,
                l.ArrivalTime,
                Math.Max(0, (int)(l.ArrivalTime - l.DepartureTime).TotalMinutes),
                l.ConfirmationCode))
            .ToList();

        var budgetSummary = BuildBudgetSummary(trip, budgetItems);

        return new TripReportDto(
            reportType,
            DateTime.UtcNow,
            tripHeader,
            reportDestinations,
            reportPlanItems,
            budgetSummary,
            reportAccommodations,
            reportTravelLegs,
            Array.Empty<string>(), // Companions — no data model backs this yet.
            null); // Reflection — set afterward via the dedicated endpoint, MemorySummary only.
    }

    private static ReportBudgetSummaryDto BuildBudgetSummary(Trip trip, List<BudgetItem> budgetItems)
    {
        var currency = trip.Currency ?? budgetItems.Select(b => b.Currency).FirstOrDefault() ?? "USD";
        var grandTotal = budgetItems.Sum(b => b.Amount);

        var categories = budgetItems
            .GroupBy(b => b.Category)
            .Select(g => (Category: g.Key, Total: g.Sum(b => b.Amount)))
            .OrderByDescending(c => c.Total)
            .Select(c => new ReportBudgetCategoryDto(
                c.Category,
                c.Total,
                grandTotal > 0 ? Math.Round(c.Total / grandTotal * 100m, 1) : 0m))
            .ToList();

        return new ReportBudgetSummaryDto(grandTotal, currency, categories);
    }
}
