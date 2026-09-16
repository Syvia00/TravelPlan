using FluentValidation;

namespace TravelPlan.Shared.DTOs.Trips;

public class CreateTripDtoValidator : AbstractValidator<CreateTripDto>
{
    public CreateTripDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("EndDate must be on or after StartDate.");

        RuleFor(x => x.Currency)
            .Length(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO code, e.g. USD.")
            .When(x => x.Currency is not null);

        RuleFor(x => x.TotalBudget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TotalBudget is not null);
    }
}
