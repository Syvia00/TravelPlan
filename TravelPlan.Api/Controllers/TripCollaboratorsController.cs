using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.TripCollaborators;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/trip-collaborators")]
public class TripCollaboratorsController : ControllerBase
{
    private readonly ITripCollaboratorRepository _collaborators;
    private readonly IUserRepository _users;
    private readonly ITripAccessService _tripAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateTripCollaboratorDto> _createValidator;
    private readonly IValidator<UpdateTripCollaboratorDto> _updateValidator;

    public TripCollaboratorsController(
        ITripCollaboratorRepository collaborators,
        IUserRepository users,
        ITripAccessService tripAccess,
        ICurrentUserService currentUser,
        IValidator<CreateTripCollaboratorDto> createValidator,
        IValidator<UpdateTripCollaboratorDto> updateValidator)
    {
        _collaborators = collaborators;
        _users = users;
        _tripAccess = tripAccess;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Viewer+ so any collaborator can see who else has access, not just the owner.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripCollaboratorDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.HasAccessAsync(tripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var collaborators = await _collaborators.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(collaborators.Select(ToDto));
    }

    /// <summary>
    /// Invite by email — owner only, even an Editor collaborator can't invite more people. If the
    /// email doesn't match an existing account, a pending placeholder User row is created and the
    /// invite sits with AcceptedAt null until that email actually signs in (UserSyncMiddleware
    /// claims the placeholder and accepts the invite at that point).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TripCollaboratorDto>> Create(CreateTripCollaboratorDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        if (!await _tripAccess.IsOwnerAsync(dto.TripId, cancellationToken))
        {
            ModelState.AddModelError(nameof(dto.TripId), "TripId does not refer to a trip you own.");
            return ValidationProblem(ModelState);
        }

        var invitedUser = await _users.FindOrCreatePendingAsync(dto.Email.Trim(), cancellationToken);

        if (invitedUser.Id == _currentUser.UserId)
        {
            ModelState.AddModelError(nameof(dto.Email), "You already own this trip.");
            return ValidationProblem(ModelState);
        }

        if (await _collaborators.FindAsync(dto.TripId, invitedUser.Id, cancellationToken) is not null)
        {
            ModelState.AddModelError(nameof(dto.Email), "This person is already invited to this trip.");
            return ValidationProblem(ModelState);
        }

        var now = DateTime.UtcNow;
        var collaborator = new TripCollaborator
        {
            TripId = dto.TripId,
            UserId = invitedUser.Id,
            Role = dto.Role,
            InvitedAt = now,
            // Already a real account (has signed in before) — no separate "accept" step needed;
            // otherwise pending until UserSyncMiddleware sees that email sign in for the first time.
            AcceptedAt = invitedUser.ExternalAuthId is not null ? now : null,
        };

        await _collaborators.AddAsync(collaborator, cancellationToken);
        await _collaborators.SaveChangesAsync(cancellationToken);

        collaborator.User = invitedUser;
        return CreatedAtAction(nameof(List), new { tripId = dto.TripId }, ToDto(collaborator));
    }

    /// <summary>Change a collaborator's role — owner only.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TripCollaboratorDto>> UpdateRole(int id, UpdateTripCollaboratorDto dto, CancellationToken cancellationToken)
    {
        var validation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        var collaborator = await _collaborators.GetByIdAsync(id, cancellationToken);
        if (collaborator is null || !await _tripAccess.IsOwnerAsync(collaborator.TripId, cancellationToken))
        {
            return NotFound();
        }

        collaborator.Role = dto.Role;
        await _collaborators.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(collaborator));
    }

    /// <summary>Revoke access — owner only.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var collaborator = await _collaborators.GetByIdAsync(id, cancellationToken);
        if (collaborator is null || !await _tripAccess.IsOwnerAsync(collaborator.TripId, cancellationToken))
        {
            return NotFound();
        }

        _collaborators.Remove(collaborator);
        await _collaborators.SaveChangesAsync(cancellationToken);

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

    private static TripCollaboratorDto ToDto(TripCollaborator c) => new(
        c.Id,
        c.TripId,
        c.UserId,
        c.User?.DisplayName ?? "",
        c.User?.Email,
        c.Role,
        c.InvitedAt,
        c.AcceptedAt);
}
