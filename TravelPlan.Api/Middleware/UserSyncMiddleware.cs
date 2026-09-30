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
///
/// The email-claiming path depends entirely on the access token actually carrying an email claim
/// — and by default, Entra External ID access tokens for a custom API scope do NOT include
/// email/name unless the API app registration explicitly adds them as optional claims (Token
/// configuration → Add optional claim → Access). Confirmed live in production (2026-09-30): a
/// real, previously-working account's access token carried "name" but no "email"/"emails" claim at
/// all, for a Google-federated sign-in — meaning every first-time sign-in currently creates a
/// disconnected User row with a null Email, and can never claim a pending email-invite placeholder,
/// regardless of how correct the matching logic itself is. That token-configuration gap is an Entra
/// tenant setting this code cannot fix or work around — see deployment-runbook.md. What the code
/// *can* fix, independent of that gap: an already-known user (matched by ExternalAuthId) previously
/// created with a null Email — because they signed up before the token carried one, or ever will —
/// stayed null forever, since email/name were only ever written at creation. That alone would mean
/// even after the tenant fix ships, every user who already signed up under the gap remains
/// unmatchable by a future email invite. SyncProfileFromClaims now runs on every sign-in, not just
/// creation, so a user's Email/DisplayName catch up as soon as the token starts carrying them.
/// </summary>
public class UserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public UserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TravelPlanDbContext db, ICurrentUserService currentUser, ILogger<UserSyncMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (context.User.Identity.AuthenticationType == ShareLinkAuthHandler.SchemeName)
            {
                await HandleShareLinkAsync(context.User, currentUser);
            }
            else
            {
                await HandleJwtUserAsync(context.User, db, currentUser, logger);
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

    internal static async Task HandleJwtUserAsync(ClaimsPrincipal principal, TravelPlanDbContext db, ICurrentUserService currentUser, ILogger logger)
    {
        var externalAuthId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalAuthId))
        {
            return;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email);
        var displayName = principal.FindFirstValue(ClaimTypes.Name);
        var now = DateTime.UtcNow;

        var user = await db.Users.SingleOrDefaultAsync(u => u.ExternalAuthId == externalAuthId);

        if (user is not null)
        {
            // Already known by ExternalAuthId — still worth catching the profile up to the current
            // token, not just at creation. A user created before this token started carrying an
            // email claim (or before an Entra token-configuration gap was fixed — see this class's
            // doc comment) would otherwise keep a null Email forever, making them permanently
            // unmatchable by a future email invite even after the underlying gap is closed.
            SyncProfileFromClaims(user, email, displayName, now);
            await db.SaveChangesAsync();
            currentUser.UserId = user.Id;
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            // No email claim at all on a first-ever sign-in: cannot look up a pending placeholder,
            // and the resulting account can never be found by a future email invite either. This is
            // the exact production gap this class's doc comment describes — logged so a Entra
            // tenant token-configuration regression is visible in the logs, not just as a silent,
            // permanently-disconnected account.
            logger.LogWarning(
                "New user (ExternalAuthId {ExternalAuthId}) signed in with no email claim on the token — " +
                "this account will not be matched to any pending email invite, and cannot be found by a " +
                "future one either. Check the API app registration's access token optional claims.",
                externalAuthId);
        }

        user = string.IsNullOrEmpty(email)
            ? null
            : await db.Users.SingleOrDefaultAsync(u => u.ExternalAuthId == null && u.Email != null && u.Email.ToLower() == email.ToLower());

        if (user is not null)
        {
            // Claiming a placeholder row created by an email invite (see
            // TripCollaboratorsController) — this is the moment "that email signs up".
            user.ExternalAuthId = externalAuthId;
            SyncProfileFromClaims(user, email, displayName, now);

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
        currentUser.UserId = user.Id;
    }

    /// <summary>
    /// Updates Email/DisplayName from the current token's claims when they add information the
    /// stored row doesn't already have — never blanks out a previously-known value just because a
    /// later token happens to omit the claim (e.g. an unrelated request shape, or a temporary IdP
    /// hiccup shouldn't erase a real email the row already earned).
    /// </summary>
    private static void SyncProfileFromClaims(User user, string? email, string? displayName, DateTime now)
    {
        var changed = false;

        if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(email))
        {
            user.Email = email;
            changed = true;
        }

        if (!string.IsNullOrEmpty(displayName) && user.DisplayName != displayName)
        {
            user.DisplayName = displayName;
            changed = true;
        }

        if (changed)
        {
            user.UpdatedAt = now;
        }
    }
}
