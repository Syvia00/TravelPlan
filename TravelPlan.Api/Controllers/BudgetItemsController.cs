using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.BudgetItems;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budget-items")]
public class BudgetItemsController : ControllerBase
{
    private readonly IBudgetItemRepository _budgetItems;
    private readonly ITripAccessService _tripAccess;
    private readonly IValidator<CreateBudgetItemDto> _createValidator;
    private readonly IValidator<UpdateBudgetItemDto> _updateValidator;

    public BudgetItemsController(
        IBudgetItemRepository budgetItems,
        ITripAccessService tripAccess,
        IValidator<CreateBudgetItemDto> createValidator,
        IValidator<UpdateBudgetItemDto> updateValidator)
    {
        _budgetItems = budgetItems;
        _tripAccess = tripAccess;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetItemDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var items = await _budgetItems.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BudgetItemDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _budgetItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetItemDto>> Create(CreateBudgetItemDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        if (!await _tripAccess.HasAccessAsync(dto.TripId, TripRole.Editor, cancellationToken))
        {
            ModelState.AddModelError(nameof(dto.TripId), "TripId does not refer to a trip you have edit access to.");
            return ValidationProblem(ModelState);
        }

        var item = new BudgetItem
        {
            TripId = dto.TripId,
            Category = dto.Category,
            Description = dto.Description,
            Amount = dto.Amount,
            Currency = dto.Currency,
            Date = dto.Date,
            IsPaid = dto.IsPaid,
        };

        await _budgetItems.AddAsync(item, cancellationToken);
        await _budgetItems.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetItemDto>> Update(int id, UpdateBudgetItemDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var item = await _budgetItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        item.Category = dto.Category;
        item.Description = dto.Description;
        item.Amount = dto.Amount;
        item.Currency = dto.Currency;
        item.Date = dto.Date;
        item.IsPaid = dto.IsPaid;

        await _budgetItems.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _budgetItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        _budgetItems.Remove(item);
        await _budgetItems.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private ActionResult ValidationProblemFor(FluentValidation.Results.ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }

    private static BudgetItemDto ToDto(BudgetItem b) => new(
        b.Id,
        b.TripId,
        b.Category,
        b.Description,
        b.Amount,
        b.Currency,
        b.Date,
        b.IsPaid);
}
