namespace TravelPlan.Api.Services;

/// <summary>
/// The single allow-list of image content types this app accepts for plan-item photo uploads, and
/// the only place the content-type -&gt; blob file-extension mapping lives. The client's stated
/// Content-Type is what both PlanItemPhotosController (rejecting anything else before it reaches
/// storage) and PhotoStorageService (naming the generated blob) key off.
/// </summary>
public static class PhotoContentTypes
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionsByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
    };

    public static bool TryGetExtension(string? contentType, out string extension)
    {
        extension = string.Empty;
        return contentType is not null && ExtensionsByContentType.TryGetValue(contentType, out extension!);
    }
}
