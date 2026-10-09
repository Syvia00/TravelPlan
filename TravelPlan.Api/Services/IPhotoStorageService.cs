using TravelPlan.Shared.DTOs.PlanItems;

namespace TravelPlan.Api.Services;

/// <summary>
/// Blob Storage is the only source of truth for plan-item photos — there is no database table.
/// A blob's path (<c>{tripId}/{planItemId}/{generatedName}</c>) is all the structure this needs:
/// listing is "everything under this prefix", and deleting a trip or plan item's photos is
/// "everything under this prefix", with no separate metadata to keep in sync. Blobs live under
/// {tripId}/{planItemId}/ rather than {userId}/{tripId}/ so a collaborator's uploads stay with
/// the trip, not the uploader.
///
/// Each photo also gets a thumbnail blob under <c>thumbs/{tripId}/{planItemId}/{sameName}</c> —
/// the same leaf name as the original, just under a different top-level prefix, so correlating
/// one to the other is pure string concatenation and ListAsync (which only ever looks under
/// {tripId}/{planItemId}/, never under thumbs/) never has to filter thumbnails out.
/// </summary>
public interface IPhotoStorageService
{
    /// <summary>Generates the blob's name server-side (a GUID plus an extension derived from
    /// <paramref name="contentType"/>) — the caller's original file name is never used as or in
    /// a blob path.</summary>
    Task<PlanItemPhotoDto> UploadAsync(int tripId, int planItemId, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Stores the pre-generated thumbnail bytes (see PhotoThumbnailGenerator) for an
    /// already-uploaded photo, under its matching thumbs/ path. Always stored as image/jpeg.</summary>
    Task UploadThumbnailAsync(int tripId, int planItemId, string photoName, byte[] content, CancellationToken cancellationToken = default);

    Task<List<PlanItemPhotoDto>> ListAsync(int tripId, int planItemId, CancellationToken cancellationToken = default);

    /// <summary>Null if no such photo exists (unknown name, or it never belonged to this trip/plan item).</summary>
    Task<PhotoContent?> DownloadAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default);

    /// <summary>Null if no such thumbnail exists.</summary>
    Task<PhotoContent?> DownloadThumbnailAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default);

    /// <summary>False if no such photo existed to delete. Also deletes the matching thumbnail,
    /// if any.</summary>
    Task<bool> DeleteAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default);

    /// <summary>Called when a PlanItem is deleted — the database cascade doesn't reach Blob
    /// Storage. Covers both the photos and their thumbnails.</summary>
    Task DeleteAllForPlanItemAsync(int tripId, int planItemId, CancellationToken cancellationToken = default);

    /// <summary>Called when a Trip is deleted — covers every plan item's photos and thumbnails in
    /// two prefix deletes, without needing to enumerate the trip's plan items first.</summary>
    Task DeleteAllForTripAsync(int tripId, CancellationToken cancellationToken = default);
}

public readonly record struct PhotoContent(Stream Content, string ContentType);
