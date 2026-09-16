using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripShareLinks;

public class CreateTripShareLinkDtoValidator : AbstractValidator<CreateTripShareLinkDto>
{
    public CreateTripShareLinkDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0);

        RuleFor(x => x.Role)
            .IsInEnum();

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("ExpiresAt must be in the future.")
            .When(x => x.ExpiresAt is not null);
    }
}
