using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripMemories;

public class UpdateTripMemoryReflectionDtoValidator : AbstractValidator<UpdateTripMemoryReflectionDto>
{
    public UpdateTripMemoryReflectionDtoValidator()
    {
        RuleFor(x => x.Reflection)
            .NotEmpty()
            .MaximumLength(5000);
    }
}
