namespace TravelPlan.Api.Services;

/// <summary>
/// The single allow-list of image content types this app accepts for plan-item photo uploads, and
/// the only place the content-type -&gt; blob file-extension mapping lives. The client's stated
/// Content-Type is what both PlanItemPhotosController (rejecting anything else before it reaches
/// storage) and PhotoStorageService (naming the generated blob) key off — but the stated
/// Content-Type is just a request header the client controls, so PlanItemPhotosController also
/// calls <see cref="HasValidSignature"/> against the file's actual bytes before trusting it: a
/// client could otherwise label any file (an HTML page, a script) "image/png" and have it stored
/// and later served back with that content-type.
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

    /// <summary>
    /// True if <paramref name="header"/> (the file's first bytes — must include at least the first
    /// 12) starts with the magic-byte signature for <paramref name="contentType"/>. False for a
    /// content type this class doesn't recognize at all, so this is always meant to run after
    /// <see cref="TryGetExtension"/> already accepted the content type.
    /// </summary>
    public static bool HasValidSignature(string contentType, ReadOnlySpan<byte> header)
    {
        switch (contentType)
        {
            // FF D8 FF — every JPEG variant (JFIF/EXIF/raw) shares this 3-byte start-of-image marker.
            case "image/jpeg":
                return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

            // 89 50 4E 47 0D 0A 1A 0A — the fixed 8-byte PNG signature.
            case "image/png":
                return header.Length >= 8 &&
                    header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                    header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

            // "GIF87a" or "GIF89a" — the only two GIF format versions.
            case "image/gif":
                return header.Length >= 6 &&
                    header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' &&
                    header[3] == (byte)'8' && (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a';

            // RIFF????WEBP — a RIFF container (bytes 4-7 are a little-endian length, not checked)
            // whose form type at offset 8 is "WEBP".
            case "image/webp":
                return header.Length >= 12 &&
                    header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                    header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

            default:
                return false;
        }
    }
}
