namespace TravelPlan.Shared.DTOs.PlanItems;

/// <summary>Name is the generated blob leaf name (not a full path or URL) — the client uses it to
/// build the download/delete routes; it never sees the blob's container or connection details.</summary>
public record PlanItemPhotoDto(string Name, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);
