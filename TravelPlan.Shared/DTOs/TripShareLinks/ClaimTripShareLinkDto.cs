namespace TravelPlan.Shared.DTOs.TripShareLinks;

/// <summary>
/// Lets a signed-in visitor turn a share link into durable, accepted collaborator access instead
/// of just viewing anonymously through it. Must be called with a normal JWT — a request carrying
/// the "X-Share-Token" header authenticates as the anonymous link identity instead (see
/// ShareLinkAuthHandler/"SmartAuth" in Program.cs), which has no UserId to attach access to.
/// </summary>
public record ClaimTripShareLinkDto(string Token);
