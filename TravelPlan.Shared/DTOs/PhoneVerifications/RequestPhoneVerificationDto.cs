namespace TravelPlan.Shared.DTOs.PhoneVerifications;

/// <summary>Kicks off the phone-OTP flow: sends an SMS code to PhoneNumber via Azure Communication Services.</summary>
public record RequestPhoneVerificationDto(
    string PhoneNumber
);
