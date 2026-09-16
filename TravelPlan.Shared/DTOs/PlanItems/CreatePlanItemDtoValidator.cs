using FluentValidation;

namespace TravelPlan.Shared.DTOs.PlanItems;

public class CreatePlanItemDtoValidator : AbstractValidator<CreatePlanItemDto>
{
    public CreatePlanItemDtoValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0);

        RuleFor(x => x.DestinationId)
            .GreaterThan(0)
            .When(x => x.DestinationId is not null);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.SourceUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("SourceUrl must be an absolute URL.")
            .When(x => x.SourceUrl is not null);

        RuleFor(x => x)
            .Must(x => x.Date is not null || x.Time is null)
            .WithMessage("Time cannot be set without a Date.")
            .WithName("Time");
    }
}
