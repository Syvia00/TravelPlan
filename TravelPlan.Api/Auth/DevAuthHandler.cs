using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TravelPlan.Api.Auth;

/// <summary>
/// Authenticates every request as a fixed dev user. Registered only in Development —
/// see README.md's "Dev-auth bypass" section. Real auth (Entra External ID + phone OTP)
/// lands in Phase 3.
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";

    public const string DevExternalAuthId = "dev-external-auth-id";
    public const string DevEmail = "dev@travelplan.local";
    public const string DevDisplayName = "Dev User";

    public DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, DevExternalAuthId),
            new Claim(ClaimTypes.Email, DevEmail),
            new Claim(ClaimTypes.Name, DevDisplayName),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
