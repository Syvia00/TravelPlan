using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripShareLinks;

public class ClaimTripShareLinkDtoValidator : AbstractValidator<ClaimTripShareLinkDto>
{
    public ClaimTripShareLinkDtoValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
    }
}
