using FluentValidation;

namespace TravelPlan.Shared.DTOs.PhoneVerifications;

public class RequestPhoneVerificationDtoValidator : AbstractValidator<RequestPhoneVerificationDto>
{
    public RequestPhoneVerificationDtoValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{1,14}$")
            .WithMessage("PhoneNumber must be in E.164 format, e.g. +15551234567.");
    }
}
