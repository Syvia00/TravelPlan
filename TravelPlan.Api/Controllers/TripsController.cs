using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.DTOs.Trips;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly ITripRepository _trips;
    private readonly ICurrentUserService _currentUser;
    private readonly ITripAccessService _tripAccess;
    private readonly ITripCompletionService _completionService;
    private readonly ITripReportService _reportService;
    private readonly IValidator<CreateTripDto> _createValidator;
    private readonly IValidator<UpdateTripDto> _updateValidator;

    public TripsController(
        ITripRepository trips,
        ICurrentUserService currentUser,
        ITripAccessService tripAccess,
        ITripCompletionService completionService,
        ITripReportService reportService,
        IValidator<CreateTripDto> createValidator,
        IValidator<UpdateTripDto> updateValidator)
    {
        _trips = trips;
        _currentUser = currentUser;
        _tripAccess = tripAccess;
        _completionService = completionService;
        _reportService = reportService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripDto>>> List(CancellationToken cancellationToken)
    {
        // "My trips" only makes sense for a real account — not a share-link visitor, who has no
        // list of trips, just access to the one their link points at.
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var trips = await _trips.ListByUserIdAsync(userId, cancellationToken);
        return Ok(trips.Select(t => ToDto(t, userId)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TripDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var access = await _tripAccess.GetAccessInfoAsync(id, cancellationToken);
        if (access is null)
        {
            return NotFound();
        }

        var trip = await _trips.GetByIdAsync(id, cancellationToken);
        return trip is null ? NotFound() : Ok(ToDto(trip, access.Value));
    }

    [HttpPost]
    public async Task<ActionResult<TripDto>> Create(CreateTripDto dto, CancellationToken cancellationToken)
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

        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            UserId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Status = Shared.Models.Enums.TripStatus.Draft,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Currency = dto.Currency,
            TotalBudget = dto.TotalBudget,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _trips.AddAsync(trip, cancellationToken);
        await _trips.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = trip.Id }, ToDto(trip, new TripAccessInfo(true, null)));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TripDto>> Update(int id, UpdateTripDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        if (!await _tripAccess.HasAccessAsync(id, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        var trip = await _trips.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        // A client can flip Status to Completed through this generic endpoint instead of the
        // dedicated /complete action below — either way, completion must generate the
        // MemorySummary report. Only fires on the transition (not on every save of an
        // already-completed trip), matching erd.md's "generated once" rule.
        var completingNow = dto.Status == TripStatus.Completed && trip.Status != TripStatus.Completed;

        trip.Title = dto.Title;
        trip.Description = dto.Description;
        trip.Status = dto.Status;
        trip.StartDate = dto.StartDate;
        trip.EndDate = dto.EndDate;
        trip.Currency = dto.Currency;
        trip.TotalBudget = dto.TotalBudget;
        trip.UpdatedAt = DateTime.UtcNow;

        await _trips.SaveChangesAsync(cancellationToken);

        if (completingNow)
        {
            await _reportService.GenerateReportForTripAsync(
                trip, TripMemoryReportType.MemorySummary, null, null, cancellationToken);
        }

        var access = await _tripAccess.GetAccessInfoAsync(id, cancellationToken);
        return Ok(ToDto(trip, access ?? new TripAccessInfo(false, null)));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<TripDto>> Complete(int id, CancellationToken cancellationToken)
    {
        var trip = await _completionService.CompleteTripAsync(id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        var access = await _tripAccess.GetAccessInfoAsync(id, cancellationToken);
        return Ok(ToDto(trip, access ?? new TripAccessInfo(false, null)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // Deleting the whole trip is owner-only — even an Editor collaborator must not be able
        // to do this (see ITripAccessService.IsOwnerAsync).
        if (!await _tripAccess.IsOwnerAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var trip = await _trips.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return NotFound();
        }

        _trips.Remove(trip);
        await _trips.SaveChangesAsync(cancellationToken);

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

    private static TripDto ToDto(Trip t, TripAccessInfo access) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.StartDate,
        t.EndDate,
        t.Currency,
        t.TotalBudget,
        t.CreatedAt,
        t.UpdatedAt,
        access.IsOwner,
        access.Role);

    /// <summary>
    /// For the bulk List response, where TripRepository.ListByUserIdAsync already eager-loads
    /// each trip's Collaborators filtered to just this user's own row (if any) — avoids an
    /// ITripAccessService round trip per trip.
    /// </summary>
    private static TripDto ToDto(Trip t, int userId)
    {
        var isOwner = t.UserId == userId;
        var role = isOwner ? null : t.Collaborators.FirstOrDefault(c => c.UserId == userId)?.Role;
        return ToDto(t, new TripAccessInfo(isOwner, role));
    }
}
