using FluentValidation;

namespace TravelPlan.Shared.DTOs.Accommodations;

public class UpdateAccommodationDtoValidator : AbstractValidator<UpdateAccommodationDto>
{
    public UpdateAccommodationDtoValidator()
    {
        RuleFor(x => x.DestinationId)
            .GreaterThan(0)
            .When(x => x.DestinationId is not null);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Address)
            .MaximumLength(500);

        RuleFor(x => x.CheckOut)
            .GreaterThan(x => x.CheckIn)
            .WithMessage("CheckOut must be after CheckIn.");

        RuleFor(x => x.ConfirmationCode)
            .MaximumLength(100);
    }
}
