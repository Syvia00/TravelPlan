using FluentValidation;

namespace TravelPlan.Shared.DTOs.BudgetItems;

public class UpdateBudgetItemDtoValidator : AbstractValidator<UpdateBudgetItemDto>
{
    public UpdateBudgetItemDtoValidator()
    {
        RuleFor(x => x.Category)
            .IsInEnum();

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter ISO code, e.g. USD.");
    }
}
