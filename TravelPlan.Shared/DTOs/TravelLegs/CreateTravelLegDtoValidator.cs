using FluentValidation;

namespace TravelPlan.Shared.DTOs.TravelLegs;

public class CreateTravelLegDtoValidator : AbstractValidator<CreateTravelLegDto>
{
    public CreateTravelLegDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0);

        RuleFor(x => x.DepartureLocation)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ArrivalLocation)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ArrivalTime)
            .GreaterThan(x => x.DepartureTime)
            .WithMessage("ArrivalTime must be after DepartureTime.");

        RuleFor(x => x.TransportType)
            .IsInEnum();

        RuleFor(x => x.ConfirmationCode)
            .MaximumLength(100);
    }
}
