using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.TripShareLinks;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Controllers;

/// <summary>
/// Anonymous share links — generating and revoking is owner-only management (same reasoning as
/// collaborator invites: even an Editor collaborator shouldn't be able to mint new access grants
/// for the trip). Viewing/editing through a consumed link isn't handled here at all — see
/// ShareLinkAuthHandler, which reads the token straight off the "X-Share-Token" header, and
/// TripsController.GetShared, which resolves the trip it points at. Claim (below) is the other
/// half of consuming a link: turning it into durable access for a signed-in visitor.
/// </summary>
[ApiController]
[Authorize]
[Route("api/trip-share-links")]
public class TripShareLinksController : ControllerBase
{
    private readonly ITripShareLinkRepository _shareLinks;
    private readonly ITripCollaboratorRepository _collaborators;
    private readonly ITripAccessService _tripAccess;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateTripShareLinkDto> _createValidator;
    private readonly IValidator<ClaimTripShareLinkDto> _claimValidator;

    public TripShareLinksController(
        ITripShareLinkRepository shareLinks,
        ITripCollaboratorRepository collaborators,
        ITripAccessService tripAccess,
        ICurrentUserService currentUser,
        IValidator<CreateTripShareLinkDto> createValidator,
        IValidator<ClaimTripShareLinkDto> claimValidator)
    {
        _shareLinks = shareLinks;
        _collaborators = collaborators;
        _tripAccess = tripAccess;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _claimValidator = claimValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripShareLinkDto>>> List([FromQuery] int tripId, CancellationToken cancellationToken)
    {
        if (!await _tripAccess.IsOwnerAsync(tripId, cancellationToken))
        {
            return NotFound();
        }

        var links = await _shareLinks.ListByTripIdAsync(tripId, cancellationToken);
        return Ok(links.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<TripShareLinkDto>> Create(CreateTripShareLinkDto dto, CancellationToken cancellationToken)
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

        var link = new TripShareLink
        {
            TripId = dto.TripId,
            Token = GenerateToken(),
            Role = dto.Role,
            ExpiresAt = dto.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
        };

        await _shareLinks.AddAsync(link, cancellationToken);
        await _shareLinks.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), new { tripId = dto.TripId }, ToDto(link));
    }

    /// <summary>
    /// Turns a share link into durable, accepted TripCollaborator access for the caller — the
    /// path from "I opened someone's link" to "this trip is in my account now". Must be called
    /// with a normal JWT (not the "X-Share-Token" header, which would authenticate as the
    /// anonymous link identity instead and leave _currentUser.UserId null). Idempotent: an owner
    /// or existing collaborator with equal-or-higher role is left alone; a lower-role collaborator
    /// is upgraded to the link's role.
    /// </summary>
    [HttpPost("claim")]
    public async Task<ActionResult<ClaimTripShareLinkResultDto>> Claim(ClaimTripShareLinkDto dto, CancellationToken cancellationToken)
    {
        var validation = await _claimValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblemFor(validation);
        }

        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var link = await _shareLinks.GetByTokenAsync(dto.Token, cancellationToken);
        if (link is null || (link.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow))
        {
            return NotFound();
        }

        if (await _tripAccess.IsOwnerAsync(link.TripId, cancellationToken))
        {
            return Ok(new ClaimTripShareLinkResultDto(link.TripId, true, null));
        }

        var existing = await _collaborators.FindAsync(link.TripId, userId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role < link.Role)
            {
                existing.Role = link.Role;
            }

            existing.AcceptedAt ??= DateTime.UtcNow;
            await _collaborators.SaveChangesAsync(cancellationToken);
            return Ok(new ClaimTripShareLinkResultDto(link.TripId, false, existing.Role));
        }

        var now = DateTime.UtcNow;
        var collaborator = new TripCollaborator
        {
            TripId = link.TripId,
            UserId = userId,
            Role = link.Role,
            InvitedAt = now,
            AcceptedAt = now,
        };

        await _collaborators.AddAsync(collaborator, cancellationToken);
        await _collaborators.SaveChangesAsync(cancellationToken);

        return Ok(new ClaimTripShareLinkResultDto(link.TripId, false, link.Role));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var link = await _shareLinks.GetByIdAsync(id, cancellationToken);
        if (link is null || !await _tripAccess.IsOwnerAsync(link.TripId, cancellationToken))
        {
            return NotFound();
        }

        _shareLinks.Remove(link);
        await _shareLinks.SaveChangesAsync(cancellationToken);

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

    // 32 random bytes, base64url-encoded (43 chars, URL-safe) — comfortably under the
    // Token column's 64-char limit and unguessable.
    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static TripShareLinkDto ToDto(TripShareLink l) => new(
        l.Id,
        l.TripId,
        l.Token,
        l.Role,
        l.ExpiresAt,
        l.CreatedAt);
}
