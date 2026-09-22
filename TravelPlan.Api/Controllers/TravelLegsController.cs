using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.TravelLegs;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/travel-legs")]
public class TravelLegsController : ControllerBase
{
    private readonly ITravelLegRepository _travelLegs;
    private readonly ITripAccessService _tripAccess;
    private readonly IValidator<CreateTravelLegDto> _createValidator;
    private readonly IValidator<UpdateTravelLegDto> _updateValidator;

    public TravelLegsController(
        ITravelLegRepository travelLegs,
        ITripAccessService tripAccess,
        IValidator<CreateTravelLegDto> createValidator,
        IValidator<UpdateTravelLegDto> updateValidator)
    {
        _travelLegs = travelLegs;
        _tripAccess = tripAccess;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TravelLegDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var items = await _travelLegs.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TravelLegDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _travelLegs.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        return Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<TravelLegDto>> Create(CreateTravelLegDto dto, CancellationToken cancellationToken)
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

        var item = new TravelLeg
        {
            TripId = dto.TripId,
            DepartureLocation = dto.DepartureLocation,
            ArrivalLocation = dto.ArrivalLocation,
            DepartureTime = dto.DepartureTime,
            ArrivalTime = dto.ArrivalTime,
            TransportType = dto.TransportType,
            ConfirmationCode = dto.ConfirmationCode,
            Notes = dto.Notes,
        };

        await _travelLegs.AddAsync(item, cancellationToken);
        await _travelLegs.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TravelLegDto>> Update(int id, UpdateTravelLegDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var item = await _travelLegs.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        item.DepartureLocation = dto.DepartureLocation;
        item.ArrivalLocation = dto.ArrivalLocation;
        item.DepartureTime = dto.DepartureTime;
        item.ArrivalTime = dto.ArrivalTime;
        item.TransportType = dto.TransportType;
        item.ConfirmationCode = dto.ConfirmationCode;
        item.Notes = dto.Notes;

        await _travelLegs.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await _travelLegs.GetByIdAsync(id, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        _travelLegs.Remove(item);
        await _travelLegs.SaveChangesAsync(cancellationToken);

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

    private static TravelLegDto ToDto(TravelLeg l) => new(
        l.Id,
        l.TripId,
        l.DepartureLocation,
        l.ArrivalLocation,
        l.DepartureTime,
        l.ArrivalTime,
        l.TransportType,
        l.ConfirmationCode,
        l.Notes);
}
