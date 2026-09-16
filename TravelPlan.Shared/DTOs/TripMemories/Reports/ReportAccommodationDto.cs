namespace TravelPlan.Shared.DTOs.TripMemories.Reports;

public record ReportAccommodationDto(
    string Name,
    string? Address,
    DateTime CheckIn,
    DateTime CheckOut,
    int Nights,
    string? ConfirmationCode
);
