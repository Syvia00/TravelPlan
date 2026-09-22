using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.PlanItems;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/plan-items")]
public class PlanItemsController : ControllerBase
{
    private readonly IPlanItemRepository _planItems;
    private readonly ITripAccessService _tripAccess;
    private readonly IValidator<CreatePlanItemDto> _createValidator;
    private readonly IValidator<UpdatePlanItemDto> _updateValidator;

    public PlanItemsController(
        IPlanItemRepository planItems,
        ITripAccessService tripAccess,
        IValidator<CreatePlanItemDto> createValidator,
        IValidator<UpdatePlanItemDto> updateValidator)
    {
        _planItems = planItems;
        _tripAccess = tripAccess;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlanItemDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var items = await _planItems.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlanItemDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<PlanItemDto>> Create(CreatePlanItemDto dto, CancellationToken cancellationToken)
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

        var item = new PlanItem
        {
            TripId = dto.TripId,
            DestinationId = dto.DestinationId,
            Date = dto.Date,
            Time = dto.Time,
            Title = dto.Title,
            Notes = dto.Notes,
            SortOrder = dto.SortOrder,
            SourceUrl = dto.SourceUrl,
            IsDone = false,
        };

        try
        {
            await _planItems.AddAsync(item, cancellationToken);
            await _planItems.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(dto.DestinationId), "DestinationId does not refer to a destination on this trip.");
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PlanItemDto>> Update(int id, UpdatePlanItemDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var item = await _planItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        item.DestinationId = dto.DestinationId;
        item.Date = dto.Date;
        item.Time = dto.Time;
        item.Title = dto.Title;
        item.Notes = dto.Notes;
        item.SortOrder = dto.SortOrder;
        item.SourceUrl = dto.SourceUrl;
        item.IsDone = dto.IsDone;

        try
        {
            await _planItems.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(dto.DestinationId), "DestinationId does not refer to a destination on this trip.");
            return ValidationProblem(ModelState);
        }

        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        _planItems.Remove(item);
        await _planItems.SaveChangesAsync(cancellationToken);

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

    private static PlanItemDto ToDto(PlanItem p) => new(
        p.Id,
        p.TripId,
        p.DestinationId,
        p.Date,
        p.Time,
        p.Title,
        p.Notes,
        p.SortOrder,
        p.SourceUrl,
        p.IsDone);
}
