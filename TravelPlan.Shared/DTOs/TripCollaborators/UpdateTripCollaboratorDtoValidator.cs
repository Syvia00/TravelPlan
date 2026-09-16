using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripCollaborators;

public class UpdateTripCollaboratorDtoValidator : AbstractValidator<UpdateTripCollaboratorDto>
{
    public UpdateTripCollaboratorDtoValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum();
    }
}
