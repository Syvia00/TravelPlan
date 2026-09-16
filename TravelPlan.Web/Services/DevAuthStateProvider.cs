using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TravelPlan.Web.Services;

/// <summary>
/// Pre-authenticated "Dev User" session — see README.md's "Dev-auth bypass" section. The
/// identity mirrors TravelPlan.Api's DevAuthHandler; the API itself ignores auth headers in
/// Development, so this only exists to satisfy CascadingAuthenticationState/AuthorizeView.
/// Real auth (Entra External ID + phone OTP) lands in Phase 3.
/// </summary>
public class DevAuthStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-external-auth-id"),
            new Claim(ClaimTypes.Email, "dev@travelplan.local"),
            new Claim(ClaimTypes.Name, "Dev User"),
        }, authenticationType: "DevAuth");

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }
}
