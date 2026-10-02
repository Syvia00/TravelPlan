using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelPlan.Api.Data;
using TravelPlan.Api.Middleware;
using TravelPlan.Api.Services;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises UserSyncMiddleware.HandleJwtUserAsync against a real SQLite database, covering the
/// email-invite claiming path this session investigated after it was flagged as broken in
/// production (Session 6.9's live verification: an invited collaborator's sign-up landed as a
/// second, disconnected account instead of claiming the pending placeholder).
///
/// The confirmed root cause, live in production (2026-09-30): Entra External ID access tokens for
/// this API's custom scope carry a "name" claim but no "email"/"emails" claim at all, for a
/// Google-federated sign-in — an access-token optional-claims gap in the API's own app
/// registration, not a bug in this matching logic. That gap is a tenant configuration issue this
/// codebase cannot fix (see UserSyncMiddleware's doc comment and deployment-runbook.md); these
/// tests instead cover what the code actually controls: the matching logic is correct when an
/// email claim IS present, and — the real, fixable bug found alongside it — a known user's Email
/// no longer stays null forever just because it was absent at account-creation time.
/// </summary>
public class UserSyncMiddlewareTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;
    private readonly ListLogger _logger = new();

    public UserSyncMiddlewareTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TravelPlanSqliteDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TravelPlanSqliteDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static ClaimsPrincipal JwtPrincipal(string externalAuthId, string? email = null, string? name = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, externalAuthId) };
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private Task HandleAsync(ClaimsPrincipal principal, ICurrentUserService currentUser, ILogger? logger = null) =>
        UserSyncMiddleware.HandleJwtUserAsync(principal, _context, currentUser, logger ?? _logger);

    [Fact]
    public async Task New_sign_in_with_email_claim_creates_a_user_with_that_email()
    {
        var currentUser = new CurrentUserService();

        await HandleAsync(JwtPrincipal("ext-new-1", email: "fresh@example.com", name: "Fresh User"), currentUser);

        var user = await _context.Users.SingleAsync();
        Assert.Equal("ext-new-1", user.ExternalAuthId);
        Assert.Equal("fresh@example.com", user.Email);
        Assert.Equal("Fresh User", user.DisplayName);
        Assert.Equal(user.Id, currentUser.UserId);
    }

    [Fact]
    public async Task First_sign_in_claims_a_pending_email_invite_placeholder_case_insensitively()
    {
        var owner = new User { DisplayName = "Owner", ExternalAuthId = "owner-ext", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var placeholder = new User
        {
            Email = "Invitee@Example.com",
            DisplayName = "Invitee@Example.com", // FindOrCreatePendingAsync sets DisplayName = email at invite time.
            ExternalAuthId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Users.AddRange(owner, placeholder);
        var trip = new Trip
        {
            User = owner,
            Title = "Trip",
            Status = TripStatus.Draft,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        var invite = new TripCollaborator
        {
            TripId = trip.Id,
            UserId = placeholder.Id,
            Role = TripRole.Viewer,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = null,
        };
        _context.TripCollaborators.Add(invite);
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();

        // Different case than the stored placeholder email — the match must be case-insensitive.
        await HandleAsync(JwtPrincipal("invitee-ext", email: "invitee@example.com", name: "Real Name"), currentUser);

        Assert.Equal(2, await _context.Users.CountAsync()); // owner + claimed placeholder, no third row.
        var claimed = await _context.Users.SingleAsync(u => u.Id == placeholder.Id);
        Assert.Equal("invitee-ext", claimed.ExternalAuthId);
        Assert.Equal("Real Name", claimed.DisplayName);
        Assert.Equal(placeholder.Id, currentUser.UserId);

        var reloadedInvite = await _context.TripCollaborators.SingleAsync(c => c.Id == invite.Id);
        Assert.NotNull(reloadedInvite.AcceptedAt);
    }

    [Fact]
    public async Task First_sign_in_with_no_email_claim_creates_a_disconnected_account_and_logs_a_warning()
    {
        // Reproduces the exact production shape (2026-09-30): the token carries a name but no
        // email/emails claim at all. Even with a pending placeholder sitting in the table for some
        // other reason, there is no email to match against, so a new, separate account is created —
        // this test documents that today's real, observed behavior, not a hypothetical.
        var placeholder = new User
        {
            Email = "someone-else@example.com",
            DisplayName = "someone-else@example.com",
            ExternalAuthId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Users.Add(placeholder);
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();
        var logger = new ListLogger();

        await HandleAsync(JwtPrincipal("no-email-ext", email: null, name: "No Email User"), currentUser, logger);

        Assert.Equal(2, await _context.Users.CountAsync());
        var created = await _context.Users.SingleAsync(u => u.ExternalAuthId == "no-email-ext");
        Assert.Null(created.Email);
        Assert.Equal("No Email User", created.DisplayName);
        Assert.Equal(created.Id, currentUser.UserId);

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("no email claim"));
    }

    [Fact]
    public async Task Returning_user_with_a_previously_null_email_is_backfilled_once_the_token_carries_one()
    {
        // The real, fixable bug found alongside the token-configuration gap: before this fix, only
        // brand-new users ever had Email/DisplayName written — an already-known user (matched by
        // ExternalAuthId) kept whatever it had at creation forever, even after Entra started
        // sending a real email claim on later sign-ins.
        var user = new User
        {
            Email = null,
            DisplayName = "New User",
            ExternalAuthId = "backfill-ext",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();

        await HandleAsync(JwtPrincipal("backfill-ext", email: "now-known@example.com", name: "Now Known"), currentUser);

        Assert.Equal(1, await _context.Users.CountAsync());
        var reloaded = await _context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("now-known@example.com", reloaded.Email);
        Assert.Equal("Now Known", reloaded.DisplayName);
        Assert.Equal(user.Id, currentUser.UserId);
    }

    [Fact]
    public async Task Backfilling_email_absorbs_a_conflicting_orphaned_placeholder_instead_of_throwing()
    {
        // Reproduces the exact production incident (2026-10-02): backfilling an already-known
        // user's email collided with Users.Email's unique index because a leftover placeholder
        // (created by an earlier invite of this same address, sent back when the real account still
        // had no email on file to match against) already held it.
        var owner = new User { DisplayName = "Owner", ExternalAuthId = "owner-ext-2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var known = new User { Email = null, DisplayName = "New User", ExternalAuthId = "known-ext", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var orphan = new User { Email = "shared@example.com", DisplayName = "shared@example.com", ExternalAuthId = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Users.AddRange(owner, known, orphan);
        var trip = new Trip
        {
            User = owner,
            Title = "Trip",
            Status = TripStatus.Draft,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        var orphanInvite = new TripCollaborator
        {
            TripId = trip.Id,
            UserId = orphan.Id,
            Role = TripRole.Viewer,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = null,
        };
        _context.TripCollaborators.Add(orphanInvite);
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();

        await HandleAsync(JwtPrincipal("known-ext", email: "shared@example.com", name: "Known Person"), currentUser);

        Assert.Equal(2, await _context.Users.CountAsync()); // owner + known — orphan absorbed, not a third row.
        Assert.False(await _context.Users.AnyAsync(u => u.Id == orphan.Id));

        var reloadedKnown = await _context.Users.SingleAsync(u => u.Id == known.Id);
        Assert.Equal("shared@example.com", reloadedKnown.Email);

        var reloadedInvite = await _context.TripCollaborators.SingleAsync(c => c.Id == orphanInvite.Id);
        Assert.Equal(known.Id, reloadedInvite.UserId);
        Assert.NotNull(reloadedInvite.AcceptedAt);
    }

    [Fact]
    public async Task Backfilling_email_drops_an_orphaned_invite_already_duplicated_on_the_real_account()
    {
        // The real account is already a collaborator on the same trip the orphan placeholder was
        // also (separately) invited to — reassigning would collide with TripCollaborators'
        // (TripId, UserId) unique index, so the now-redundant orphan invite is dropped instead.
        var owner = new User { DisplayName = "Owner", ExternalAuthId = "owner-ext-3", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var known = new User { Email = null, DisplayName = "New User", ExternalAuthId = "known-ext-2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var orphan = new User { Email = "dup@example.com", DisplayName = "dup@example.com", ExternalAuthId = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.Users.AddRange(owner, known, orphan);
        var trip = new Trip
        {
            User = owner,
            Title = "Trip",
            Status = TripStatus.Draft,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.AddRange(
            new TripCollaborator { TripId = trip.Id, UserId = known.Id, Role = TripRole.Editor, InvitedAt = DateTime.UtcNow, AcceptedAt = DateTime.UtcNow },
            new TripCollaborator { TripId = trip.Id, UserId = orphan.Id, Role = TripRole.Viewer, InvitedAt = DateTime.UtcNow, AcceptedAt = null });
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();

        await HandleAsync(JwtPrincipal("known-ext-2", email: "dup@example.com", name: "Known Person"), currentUser);

        Assert.False(await _context.Users.AnyAsync(u => u.Id == orphan.Id));

        var remaining = await _context.TripCollaborators.Where(c => c.TripId == trip.Id).ToListAsync();
        var single = Assert.Single(remaining);
        Assert.Equal(known.Id, single.UserId);
        Assert.Equal(TripRole.Editor, single.Role); // the real account's own pre-existing invite, untouched.
    }

    [Fact]
    public async Task Returning_user_with_a_known_email_is_not_blanked_by_a_token_missing_the_claim()
    {
        var user = new User
        {
            Email = "already-known@example.com",
            DisplayName = "Already Known",
            ExternalAuthId = "stable-ext",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var currentUser = new CurrentUserService();

        // A request whose token happens not to carry email/name this time — should never erase
        // what's already on file.
        await HandleAsync(JwtPrincipal("stable-ext", email: null, name: null), currentUser);

        var reloaded = await _context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("already-known@example.com", reloaded.Email);
        Assert.Equal("Already Known", reloaded.DisplayName);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
