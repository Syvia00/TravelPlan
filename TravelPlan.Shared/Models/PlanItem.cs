namespace TravelPlan.Shared.Models;

/// <summary>
/// Unified itinerary + to-do item. No Date = trip-level backlog. Date, no Time = shows as
/// "unscheduled" on that day. Date + Time = placed on the day-view timeline.
/// </summary>
public class PlanItem
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public int? DestinationId { get; set; }

    public DateOnly? Date { get; set; }

    public TimeOnly? Time { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Origin link for quick-capture items.</summary>
    public string? SourceUrl { get; set; }

    public bool IsDone { get; set; }

    public Trip? Trip { get; set; }

    public Destination? Destination { get; set; }
}
