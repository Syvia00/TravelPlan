namespace TravelPlan.Shared.DTOs.Users;

public record UserDto(
    int Id,
    string? Email,
    string? PhoneNumber,
    DateTime? PhoneVerifiedAt,
    string DisplayName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
