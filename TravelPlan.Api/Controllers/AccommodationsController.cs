using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.Accommodations;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accommodations")]
public class AccommodationsController : ControllerBase
{
    private readonly IAccommodationRepository _accommodations;
    private readonly ITripRepository _trips;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateAccommodationDto> _createValidator;
    private readonly IValidator<UpdateAccommodationDto> _updateValidator;

    public AccommodationsController(
        IAccommodationRepository accommodations,
        ITripRepository trips,
        ICurrentUserService currentUser,
        IValidator<CreateAccommodationDto> createValidator,
        IValidator<UpdateAccommodationDto> updateValidator)
    {
        _accommodations = accommodations;
        _trips = trips;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccommodationDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var trip = await _trips.GetByIdForUserAsync(tripId, userId, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var items = await _accommodations.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccommodationDto>> GetById(int id, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var item = await _accommodations.GetByIdForUserAsync(id, userId, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<AccommodationDto>> Create(CreateAccommodationDto dto, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var trip = await _trips.GetByIdForUserAsync(dto.TripId, userId, cancellationToken);
        if (trip is null)
        {
            ModelState.AddModelError(nameof(dto.TripId), "TripId does not refer to a trip you own.");
            return ValidationProblem(ModelState);
        }

        var item = new Accommodation
        {
            TripId = dto.TripId,
            DestinationId = dto.DestinationId,
            Name = dto.Name,
            Address = dto.Address,
            CheckIn = dto.CheckIn,
            CheckOut = dto.CheckOut,
            ConfirmationCode = dto.ConfirmationCode,
            Notes = dto.Notes,
        };

        try
        {
            await _accommodations.AddAsync(item, cancellationToken);
            await _accommodations.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(dto.DestinationId), "DestinationId does not refer to a destination on this trip.");
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AccommodationDto>> Update(int id, UpdateAccommodationDto dto, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var item = await _accommodations.GetByIdForUserAsync(id, userId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.DestinationId = dto.DestinationId;
        item.Name = dto.Name;
        item.Address = dto.Address;
        item.CheckIn = dto.CheckIn;
        item.CheckOut = dto.CheckOut;
        item.ConfirmationCode = dto.ConfirmationCode;
        item.Notes = dto.Notes;

        try
        {
            await _accommodations.SaveChangesAsync(cancellationToken);
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
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var item = await _accommodations.GetByIdForUserAsync(id, userId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _accommodations.Remove(item);
        await _accommodations.SaveChangesAsync(cancellationToken);

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

    private static AccommodationDto ToDto(Accommodation a) => new(
        a.Id,
        a.TripId,
        a.DestinationId,
        a.Name,
        a.Address,
        a.CheckIn,
        a.CheckOut,
        a.ConfirmationCode,
        a.Notes);
}
