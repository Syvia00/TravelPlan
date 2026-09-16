using FluentValidation;

namespace TravelPlan.Shared.DTOs.TripMemories;

public class CreateTripMemoryDtoValidator : AbstractValidator<CreateTripMemoryDto>
{
    public CreateTripMemoryDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0);

        RuleFor(x => x.ReportType)
            .IsInEnum();

        RuleFor(x => x.Title)
            .MaximumLength(200);
    }
}
