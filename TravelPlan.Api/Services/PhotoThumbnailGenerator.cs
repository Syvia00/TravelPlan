using SkiaSharp;

namespace TravelPlan.Api.Services;

/// <summary>
/// Generates the small JPEG the day-view strip actually loads (see PlanItemPhotosController) —
/// the full-size original is only fetched when a user opens a photo. Always re-encodes as JPEG
/// regardless of the original format, since a thumbnail doesn't need to preserve e.g. PNG
/// transparency or GIF animation.
/// </summary>
public static class PhotoThumbnailGenerator
{
    public const int MaxDimensionPx = 400;
    private const int JpegQuality = 80;

    /// <summary>Decodes <paramref name="original"/> and returns a JPEG whose longest side is at
    /// most <see cref="MaxDimensionPx"/>, aspect ratio preserved. Never upscales an image that's
    /// already smaller than that. Throws if the bytes can't be decoded as an image — the caller
    /// (PlanItemPhotosController) treats that as an upload failure, not a silently-skipped
    /// thumbnail, since the day-view strip has nothing else to show for this photo.</summary>
    public static byte[] Generate(byte[] original)
    {
        using var bitmap = SKBitmap.Decode(original) ?? throw new InvalidOperationException("Could not decode the image for thumbnailing.");

        var longestSide = Math.Max(bitmap.Width, bitmap.Height);
        var scale = Math.Min(1.0, (double)MaxDimensionPx / longestSide);

        using var toEncode = scale < 1.0 ? Resize(bitmap, scale) : bitmap;
        using var image = SKImage.FromBitmap(toEncode);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }

    private static SKBitmap Resize(SKBitmap bitmap, double scale)
    {
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));

        return bitmap.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("Could not resize the image for thumbnailing.");
    }
}
