using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelPlan.Api.Data;

namespace TravelPlan.Api.Auth;

/// <summary>
/// Authenticates an anonymous visitor via the "X-Share-Token" header instead of a JWT — no
/// account needed. On success, the principal carries the share link's TripId/Role as custom
/// claims (not ClaimTypes.NameIdentifier — there's no user), which UserSyncMiddleware reads into
/// ICurrentUserService.ShareLinkTripId/ShareLinkRole for ITripAccessService to key off. Selected
/// by Program.cs's "SmartAuth" policy scheme whenever that header is present.
/// </summary>
public class ShareLinkAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ShareLink";
    public const string HeaderName = "X-Share-Token";

    public const string TripIdClaimType = "tp_share_trip_id";
    public const string RoleClaimType = "tp_share_role";

    private readonly TravelPlanDbContext _context;

    public ShareLinkAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TravelPlanDbContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var tokenValues) || string.IsNullOrWhiteSpace(tokenValues.ToString()))
        {
            return AuthenticateResult.NoResult();
        }

        var token = tokenValues.ToString();

        var link = await _context.TripShareLinks.SingleOrDefaultAsync(l => l.Token == token);
        if (link is null)
        {
            return AuthenticateResult.Fail("Unknown share link.");
        }

        if (link.ExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Share link has expired.");
        }

        var claims = new[]
        {
            new Claim(TripIdClaimType, link.TripId.ToString()),
            new Claim(RoleClaimType, link.Role.ToString()),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
