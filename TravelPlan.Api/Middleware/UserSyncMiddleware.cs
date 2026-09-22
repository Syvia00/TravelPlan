using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Auth;
using TravelPlan.Api.Data;
using TravelPlan.Api.Services;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Api.Middleware;

/// <summary>
/// Populates ICurrentUserService for the rest of the request pipeline — the one place that turns
/// "however this request got authenticated" into the shape ITripAccessService reads. Two cases:
///
/// 1. A real signed-in user (JWT): upserts the local Users row for the ExternalAuthId claim, and
///    sets ICurrentUserService.UserId. If ExternalAuthId isn't found but a *pending* placeholder
///    row exists for this email (created by inviting someone who hadn't signed up yet — see
///    TripCollaboratorsController), claims it: attaches the real ExternalAuthId/DisplayName and
///    accepts any TripCollaborators invites still waiting on it, instead of creating a duplicate
///    User row that the pending invites would never resolve to.
///
/// 2. An anonymous share-link visitor (ShareLinkAuthHandler): sets
///    ICurrentUserService.ShareLinkTripId/ShareLinkRole from its claims. No Users row involved.
///
/// Program.cs normalizes Entra External ID's raw JWT claims ("oid"/"email"/"name") onto the
/// standard ClaimTypes.* ones once, in JwtBearerEvents.OnTokenValidated, so this middleware
/// doesn't need to know the token's actual shape.
/// </summary>
public class UserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public UserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TravelPlanDbContext db, ICurrentUserService currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.User.Identity.AuthenticationType == ShareLinkAuthHandler.SchemeName)
            {
                await HandleShareLinkAsync(context.User, currentUser);
            }
            else
            {
                await HandleJwtUserAsync(context.User, db, currentUser);
            }
        }

        await _next(context);
    }

    private static Task HandleShareLinkAsync(ClaimsPrincipal user, ICurrentUserService currentUser)
    {
        var tripIdClaim = user.FindFirstValue(ShareLinkAuthHandler.TripIdClaimType);
        var roleClaim = user.FindFirstValue(ShareLinkAuthHandler.RoleClaimType);

        if (int.TryParse(tripIdClaim, out var tripId) && Enum.TryParse<TripRole>(roleClaim, out var role))
        {
            currentUser.ShareLinkTripId = tripId;
            currentUser.ShareLinkRole = role;
        }

        return Task.CompletedTask;
    }

    private static async Task HandleJwtUserAsync(ClaimsPrincipal principal, TravelPlanDbContext db, ICurrentUserService currentUser)
    {
        var externalAuthId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalAuthId))
        {
            return;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.ExternalAuthId == externalAuthId);
        var now = DateTime.UtcNow;

        if (user is null)
        {
            var email = principal.FindFirstValue(ClaimTypes.Email);
            var displayName = principal.FindFirstValue(ClaimTypes.Name);

            user = string.IsNullOrEmpty(email)
                ? null
                : await db.Users.SingleOrDefaultAsync(u => u.ExternalAuthId == null && u.Email != null && u.Email.ToLower() == email.ToLower());

            if (user is not null)
            {
                // Claiming a placeholder row created by an email invite (see
                // TripCollaboratorsController) — this is the moment "that email signs up".
                user.ExternalAuthId = externalAuthId;
                user.DisplayName = displayName ?? user.DisplayName;
                user.UpdatedAt = now;

                var pendingInvites = await db.TripCollaborators
                    .Where(c => c.UserId == user.Id && c.AcceptedAt == null)
                    .ToListAsync();
                foreach (var invite in pendingInvites)
                {
                    invite.AcceptedAt = now;
                }
            }
            else
            {
                user = new User
                {
                    ExternalAuthId = externalAuthId,
                    Email = email,
                    DisplayName = displayName ?? "New User",
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Users.Add(user);
            }

            await db.SaveChangesAsync();
        }

        currentUser.UserId = user.Id;
    }
}
