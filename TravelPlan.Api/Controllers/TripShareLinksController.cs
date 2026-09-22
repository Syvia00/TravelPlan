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
/// for the trip). Consuming a link (the anonymous visitor's side) isn't handled here at all — see
/// ShareLinkAuthHandler, which reads the token straight off the "X-Share-Token" header.
/// </summary>
[ApiController]
[Authorize]
[Route("api/trip-share-links")]
public class TripShareLinksController : ControllerBase
{
    private readonly ITripShareLinkRepository _shareLinks;
    private readonly ITripAccessService _tripAccess;
    private readonly IValidator<CreateTripShareLinkDto> _createValidator;

    public TripShareLinksController(
        ITripShareLinkRepository shareLinks,
        ITripAccessService tripAccess,
        IValidator<CreateTripShareLinkDto> createValidator)
    {
        _shareLinks = shareLinks;
        _tripAccess = tripAccess;
        _createValidator = createValidator;
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
