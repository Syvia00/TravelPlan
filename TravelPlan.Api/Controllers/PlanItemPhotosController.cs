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

        await using var stream = file.OpenReadStream();
        var photo = await _photoStorage.UploadAsync(item.TripId, planItemId, stream, file.ContentType, cancellationToken);
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
