using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TravelPlan.Shared.DTOs.PlanItems;

namespace TravelPlan.Api.Services;

/// <summary>
/// Thin wrapper around Azure Blob Storage — AzureStorage:ConnectionString never reaches a client;
/// every call here is server-to-server, and photo bytes always flow back through
/// PlanItemPhotosController (see IPhotoStorageService), never as a direct blob URL.
///
/// Registered as a singleton (see Program.cs) — BlobContainerClient is thread-safe and meant to be
/// reused, so the client is built once, the first time anything actually needs it, and cached for
/// the life of the process rather than constructed fresh on every call. It's still built lazily
/// (via Lazy&lt;T&gt;, not eagerly in the constructor) so a missing AzureStorage:ConnectionString
/// fails the first real request that touches storage rather than failing DI/startup — matching
/// MapsService's pattern of not failing startup over a Phase 2+ feature's config being unset.
/// Lazy&lt;T&gt;'s default mode caches a thrown exception and rethrows it on every subsequent
/// access, so once this has failed once it keeps failing for the rest of the process's life, not
/// just until the setting is fixed. That's fine here: fixing AzureStorage:ConnectionString is an
/// App Service setting change, and those always restart the app, which rebuilds this singleton
/// (and its Lazy&lt;T&gt;) from scratch — there's no live-fix-without-restart path this would need
/// to support.
/// </summary>
public class PhotoStorageService : IPhotoStorageService
{
    private readonly ILogger<PhotoStorageService> _logger;
    private readonly Lazy<BlobContainerClient> _container;

    public PhotoStorageService(IConfiguration configuration, ILogger<PhotoStorageService> logger)
    {
        _logger = logger;
        _container = new Lazy<BlobContainerClient>(() => BuildContainer(configuration));
    }

    public async Task<PlanItemPhotoDto> UploadAsync(int tripId, int planItemId, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!PhotoContentTypes.TryGetExtension(contentType, out var extension))
        {
            throw new ArgumentException($"Unsupported content type: {contentType}", nameof(contentType));
        }

        var container = GetContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var photoName = $"{Guid.NewGuid():N}{extension}";
        var blobClient = container.GetBlobClient(BlobPath(tripId, planItemId, photoName));

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        return new PlanItemPhotoDto(photoName, contentType, properties.Value.ContentLength, properties.Value.LastModified);
    }

    public async Task UploadThumbnailAsync(int tripId, int planItemId, string photoName, byte[] content, CancellationToken cancellationToken = default)
    {
        var container = GetContainer();
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = container.GetBlobClient(ThumbPath(tripId, planItemId, photoName));
        var headers = new BlobHttpHeaders { ContentType = "image/jpeg" };
        using var stream = new MemoryStream(content);
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);
    }

    public async Task<List<PlanItemPhotoDto>> ListAsync(int tripId, int planItemId, CancellationToken cancellationToken = default)
    {
        var container = GetContainer();
        var prefix = Prefix(tripId, planItemId);
        var results = new List<PlanItemPhotoDto>();

        try
        {
            await foreach (var blobItem in container.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken))
            {
                var name = blobItem.Name[prefix.Length..];
                results.Add(new PlanItemPhotoDto(
                    name,
                    blobItem.Properties.ContentType ?? "application/octet-stream",
                    blobItem.Properties.ContentLength ?? 0,
                    blobItem.Properties.LastModified ?? DateTimeOffset.MinValue));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The container doesn't exist yet — nothing has ever been uploaded.
        }

        return results.OrderBy(p => p.UploadedAt).ToList();
    }

    public Task<PhotoContent?> DownloadAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default) =>
        DownloadBlobAsync(BlobPath(tripId, planItemId, photoName), photoName, cancellationToken);

    public Task<PhotoContent?> DownloadThumbnailAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default) =>
        DownloadBlobAsync(ThumbPath(tripId, planItemId, photoName), photoName, cancellationToken);

    private async Task<PhotoContent?> DownloadBlobAsync(string blobPath, string photoName, CancellationToken cancellationToken)
    {
        if (!IsSafePhotoName(photoName))
        {
            return null;
        }

        var blobClient = GetContainer().GetBlobClient(blobPath);
        try
        {
            var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new PhotoContent(download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(int tripId, int planItemId, string photoName, CancellationToken cancellationToken = default)
    {
        if (!IsSafePhotoName(photoName))
        {
            return false;
        }

        var container = GetContainer();
        var response = await container.GetBlobClient(BlobPath(tripId, planItemId, photoName)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        await container.GetBlobClient(ThumbPath(tripId, planItemId, photoName)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    public Task DeleteAllForPlanItemAsync(int tripId, int planItemId, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            DeleteAllWithPrefixAsync(Prefix(tripId, planItemId), cancellationToken),
            DeleteAllWithPrefixAsync(ThumbPrefix(tripId, planItemId), cancellationToken));

    public Task DeleteAllForTripAsync(int tripId, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            DeleteAllWithPrefixAsync($"{tripId}/", cancellationToken),
            DeleteAllWithPrefixAsync($"thumbs/{tripId}/", cancellationToken));

    private async Task DeleteAllWithPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        var container = GetContainer();
        try
        {
            await foreach (var blobItem in container.GetBlobsAsync(traits: BlobTraits.None, states: BlobStates.None, prefix: prefix, cancellationToken: cancellationToken))
            {
                await container.GetBlobClient(blobItem.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The container doesn't exist yet — nothing to clean up.
        }
    }

    private BlobContainerClient GetContainer() => _container.Value;

    private BlobContainerClient BuildContainer(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("AzureStorage:ConnectionString is not configured — photo storage is unavailable.");
            throw new InvalidOperationException("AzureStorage:ConnectionString is not configured.");
        }

        var containerName = configuration["AzureStorage:ContainerName"] ?? "trip-photos";
        return new BlobContainerClient(connectionString, containerName);
    }

    private static string Prefix(int tripId, int planItemId) => $"{tripId}/{planItemId}/";

    private static string ThumbPrefix(int tripId, int planItemId) => $"thumbs/{Prefix(tripId, planItemId)}";

    private static string BlobPath(int tripId, int planItemId, string photoName) => $"{Prefix(tripId, planItemId)}{photoName}";

    private static string ThumbPath(int tripId, int planItemId, string photoName) => $"{ThumbPrefix(tripId, planItemId)}{photoName}";

    // photoName is attacker-controlled (a route segment) and gets concatenated straight into a blob
    // path — this is what stops a crafted "../../other-trip/x.jpg"-style name from escaping the
    // {tripId}/{planItemId}/ prefix. Every name this service itself generates (UploadAsync) matches
    // this exactly, so legitimate names always pass.
    private static bool IsSafePhotoName(string photoName) =>
        !string.IsNullOrEmpty(photoName) &&
        photoName.Length < 64 &&
        !photoName.Contains('/') &&
        !photoName.Contains('\\') &&
        !photoName.Contains("..", StringComparison.Ordinal);
}
