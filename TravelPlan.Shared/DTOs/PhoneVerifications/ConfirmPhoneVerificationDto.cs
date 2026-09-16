namespace TravelPlan.Shared.DTOs.PhoneVerifications;

/// <summary>Confirms the SMS code sent by a prior RequestPhoneVerificationDto call.</summary>
public record ConfirmPhoneVerificationDto(
    string PhoneNumber,
    string Code
);
