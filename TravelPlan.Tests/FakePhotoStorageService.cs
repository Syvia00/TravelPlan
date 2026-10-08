using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.PlanItems;

namespace TravelPlan.Tests;

/// <summary>
/// Minimal stand-in for Blob Storage — keeps uploaded bytes in memory and records every
/// DeleteAllFor*Async call, so tests can assert on cascade cleanup without a real Azure Storage
/// account. Shared by PlanItemPhotosControllerTests, PlanItemsControllerTests and
/// TripsControllerTests.
/// </summary>
public class FakePhotoStorageService : IPhotoStorageService
{
    private record StoredPhoto(int TripId, int PlanItemId, string ContentType, byte[] Content, DateTimeOffset UploadedAt);

    private readonly Dictionary<string, StoredPhoto> _photos = new();

    public List<(int TripId, int PlanItemId)> DeletedForPlanItemCalls { get; } = [];

    public List<int> DeletedForTripCalls { get; } = [];

    public Task<PlanItemPhotoDto> UploadAsync(int tripId, int planItemId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var bytes = buffer.ToArray();

        var name = Guid.NewGuid().ToString("N");
        var uploadedAt = DateTimeOffset.UtcNow;
        _photos[name] = new StoredPhoto(tripId, planItemId, contentType, bytes, uploadedAt);

        return Task.FromResult(new PlanItemPhotoDto(name, contentType, bytes.Length, uploadedAt));
    }

    public Task<List<PlanItemPhotoDto>> ListAsync(int tripId, int planItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_photos
            .Where(kv => kv.Value.TripId == tripId && kv.Value.PlanItemId == planItemId)
            .Select(kv => new PlanItemPhotoDto(kv.Key, kv.Value.ContentType, kv.Value.Content.Length, kv.Value.UploadedAt))
            .OrderBy(p => p.UploadedAt)
            .ToList());

    public Task<PhotoContent?> DownloadAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default)
    {
        if (!_photos.TryGetValue(photoName, out var photo) || photo.TripId != tripId || photo.PlanItemId != planItemId)
        {
            return Task.FromResult<PhotoContent?>(null);
        }

        return Task.FromResult<PhotoContent?>(new PhotoContent(new MemoryStream(photo.Content), photo.ContentType));
    }

    public Task<bool> DeleteAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default)
    {
        if (!_photos.TryGetValue(photoName, out var photo) || photo.TripId != tripId || photo.PlanItemId != planItemId)
        {
            return Task.FromResult(false);
        }

        _photos.Remove(photoName);
        return Task.FromResult(true);
    }

    public Task DeleteAllForPlanItemAsync(int tripId, int planItemId, CancellationToken cancellationToken = default)
    {
        DeletedForPlanItemCalls.Add((tripId, planItemId));
        foreach (var key in _photos.Where(kv => kv.Value.TripId == tripId && kv.Value.PlanItemId == planItemId).Select(kv => kv.Key).ToList())
        {
            _photos.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAllForTripAsync(int tripId, CancellationToken cancellationToken = default)
    {
        DeletedForTripCalls.Add(tripId);
        foreach (var key in _photos.Where(kv => kv.Value.TripId == tripId).Select(kv => kv.Key).ToList())
        {
            _photos.Remove(key);
        }

        return Task.CompletedTask;
    }
}
