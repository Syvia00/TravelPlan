using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripCollaborators;

public class CreateTripCollaboratorDtoValidator : AbstractValidator<CreateTripCollaboratorDto>
{
    public CreateTripCollaboratorDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Role)
            .IsInEnum();
    }
}
