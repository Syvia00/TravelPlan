using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Shared.Models;

public class TripMemory
{
    public int Id { get; set; }

    public int TripId { get; set; }

    public int UserId { get; set; }

    public TripMemoryReportType ReportType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Serialized TripReportDto JSON payload.</summary>
    public string ReportData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Trip? Trip { get; set; }

    public User? User { get; set; }
}
