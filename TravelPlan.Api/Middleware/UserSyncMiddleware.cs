using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Api.Services;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Middleware;

/// <summary>
/// Upserts the local Users row for the authenticated identity's ExternalAuthId and resolves
/// ICurrentUserService.UserId for the rest of the request pipeline. Reads only the standard
/// ClaimTypes.* claims — Program.cs normalizes Entra External ID's raw JWT claims ("oid"/
/// "email"/"name") onto them once, in the JwtBearerEvents.OnTokenValidated handler, so this
/// middleware doesn't need to know the token's actual shape.
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
            var externalAuthId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(externalAuthId))
            {
                var user = await db.Users.SingleOrDefaultAsync(u => u.ExternalAuthId == externalAuthId);

                if (user is null)
                {
                    var now = DateTime.UtcNow;
                    user = new User
                    {
                        ExternalAuthId = externalAuthId,
                        Email = context.User.FindFirstValue(ClaimTypes.Email),
                        DisplayName = context.User.FindFirstValue(ClaimTypes.Name) ?? "New User",
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                currentUser.UserId = user.Id;
            }
        }

        await _next(context);
    }
}
