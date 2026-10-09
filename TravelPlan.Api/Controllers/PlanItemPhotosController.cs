using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.PlanItems;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/plan-items/{planItemId:int}/photos")]
public class PlanItemPhotosController : ControllerBase
{
    private readonly IPlanItemRepository _planItems;
    private readonly ITripAccessService _tripAccess;
    private readonly IPhotoStorageService _photoStorage;

    public PlanItemPhotosController(
        IPlanItemRepository planItems,
        ITripAccessService tripAccess,
        IPhotoStorageService photoStorage)
    {
        _planItems = planItems;
        _tripAccess = tripAccess;
        _photoStorage = photoStorage;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PlanItemPhotoDto>>> List(int planItemId, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(planItemId, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var photos = await _photoStorage.ListAsync(item.TripId, planItemId, cancellationToken);
        return Ok(photos);
    }

    [HttpGet("{photoName}")]
    public async Task<IActionResult> Download(int planItemId, string photoName, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(planItemId, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Viewer, cancellationToken))
        {
            return NotFound();
        }

        var content = await _photoStorage.DownloadAsync(item.TripId, planItemId, photoName, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        // The content-type here was validated against the file's actual bytes at upload time (see
        // Upload below) — nosniff tells the browser to trust it exactly as given, rather than
        // sniffing the body and potentially treating it as something else (e.g. HTML) if a browser's
        // sniffing heuristic ever disagreed with our own validation.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Value.Content, content.Value.ContentType);
    }

    [HttpPost]
    public async Task<ActionResult<PlanItemPhotoDto>> Upload(int planItemId, IFormFile? file, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(planItemId, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "A photo file is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > PhotoContentTypes.MaxSizeBytes)
        {
            ModelState.AddModelError(nameof(file), "Photos must be 5 MB or smaller.");
            return ValidationProblem(ModelState);
        }

        if (!PhotoContentTypes.TryGetExtension(file.ContentType, out _))
        {
            ModelState.AddModelError(nameof(file), "Only JPEG, PNG, GIF or WebP images are accepted.");
            return ValidationProblem(ModelState);
        }

        // Buffer the whole file (capped at 5 MB above, so this is bounded) rather than trusting the
        // client's Content-Type header alone — that header is just request metadata the client
        // controls. Checking the actual bytes against the format's magic-byte signature is what
        // stops a client from labelling an arbitrary file "image/png" and having it stored and later
        // served back under that content type (see PhotoContentTypes.HasValidSignature).
        using var buffer = new MemoryStream();
        await using (var stream = file.OpenReadStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken);
        }

        if (!PhotoContentTypes.HasValidSignature(file.ContentType, buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))
        {
            ModelState.AddModelError(nameof(file), "The file's contents don't match its declared image type.");
            return ValidationProblem(ModelState);
        }

        buffer.Position = 0;
        var photo = await _photoStorage.UploadAsync(item.TripId, planItemId, buffer, file.ContentType, cancellationToken);
        return Ok(photo);
    }

    [HttpDelete("{photoName}")]
    public async Task<IActionResult> Delete(int planItemId, string photoName, CancellationToken cancellationToken)
    {
        var item = await _planItems.GetByIdAsync(planItemId, cancellationToken);
        if (item is null || !await _tripAccess.HasAccessAsync(item.TripId, TripRole.Editor, cancellationToken))
        {
            return NotFound();
        }

        var deleted = await _photoStorage.DeleteAsync(item.TripId, planItemId, photoName, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
