using FluentValidation;

namespace TravelPlan.Shared.DTOs.PhoneVerifications;

public class ConfirmPhoneVerificationDtoValidator : AbstractValidator<ConfirmPhoneVerificationDto>
{
    public ConfirmPhoneVerificationDtoValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{1,14}$")
            .WithMessage("PhoneNumber must be in E.164 format, e.g. +15551234567.");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Code must be a 6-digit numeric OTP.");
    }
}
