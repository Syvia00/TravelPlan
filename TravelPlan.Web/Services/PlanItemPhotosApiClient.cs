using System.Net.Http.Headers;
using TravelPlan.Shared.DTOs.PlanItems;

namespace TravelPlan.Web.Services;

public class PlanItemPhotosApiClient : ApiClientBase
{
    public PlanItemPhotosApiClient(HttpClient http) : base(http)
    {
    }

    public Task<List<PlanItemPhotoDto>> ListAsync(int planItemId, CancellationToken cancellationToken = default) =>
        GetAsync<List<PlanItemPhotoDto>>($"api/plan-items/{planItemId}/photos", cancellationToken);

    public Task<PlanItemPhotoDto> UploadAsync(int planItemId, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(fileContent, "file", "photo");

        return PostMultipartAsync<PlanItemPhotoDto>($"api/plan-items/{planItemId}/photos", form, cancellationToken);
    }

    /// <summary>Fetches the raw bytes through the authenticated API client — never a direct blob
    /// URL — so the caller can build a data: URL for an &lt;img&gt; tag.</summary>
    public Task<byte[]> DownloadAsync(int planItemId, string photoName, CancellationToken cancellationToken = default) =>
        GetBytesAsync($"api/plan-items/{planItemId}/photos/{photoName}", cancellationToken);

    public Task DeleteAsync(int planItemId, string photoName, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/plan-items/{planItemId}/photos/{photoName}", cancellationToken);
}
