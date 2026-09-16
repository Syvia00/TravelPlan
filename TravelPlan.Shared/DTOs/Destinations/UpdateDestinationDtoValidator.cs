using FluentValidation;

namespace TravelPlan.Shared.DTOs.Destinations;

public class UpdateDestinationDtoValidator : AbstractValidator<UpdateDestinationDto>
{
    public UpdateDestinationDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2, 3)
            .Matches("^[A-Z]{2,3}$")
            .WithMessage("CountryCode must be an uppercase ISO 3166-1 alpha-2/3 code.");

        RuleFor(x => x.ExitDate)
            .GreaterThanOrEqualTo(x => x.EntryDate)
            .When(x => x.ExitDate is not null)
            .WithMessage("ExitDate must be on or after EntryDate.");
    }
}
