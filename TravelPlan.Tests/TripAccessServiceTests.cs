using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Api.Services;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises TripAccessService.HasAccessAsync against a real SQLite database, not EF Core's
/// InMemory provider — the Session 6.8 bug only existed because a role comparison written inside
/// a LINQ-to-Entities query got translated into actual SQL against a (then text-typed) column;
/// EF's InMemory provider never generates SQL at all, so it could never have caught that class of
/// regression, and wouldn't catch it again if a future comparison reintroduced it. Each test opens
/// its own throwaway in-memory SQLite database and builds the schema straight from the current
/// model (EnsureCreated), not via the migration history — migration correctness (both providers,
/// applied to a fresh database, no drift) is already verified separately in this same CI workflow.
///
/// Every test name states an allow or a reject outcome explicitly — the reject cases are exactly
/// what the original bug got backwards, so they matter at least as much as the allow cases here.
/// </summary>
public class TripAccessServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;

    public TripAccessServiceTests()
    {
        // A SQLite ":memory:" database only exists for as long as its connection stays open, and
        // EF closes/reopens connections between operations by default — so the connection itself
        // (not just the context) has to be held open for the schema and data to survive from
        // EnsureCreated() through to the assertions.
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

    private static User NewUser(string displayName) => new()
    {
        DisplayName = displayName,
        ExternalAuthId = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // Sets the User navigation, not UserId directly — owner.Id is still 0 (unsaved) at the point
    // callers construct this, so EF needs the navigation reference to fix up the real FK once both
    // are saved in the same SaveChangesAsync call, regardless of which one it inserts first.
    private static Trip NewTrip(User owner) => new()
    {
        User = owner,
        Title = "Test Trip",
        Status = TripStatus.Draft,
        StartDate = DateOnly.FromDateTime(DateTime.Today),
        EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static ICurrentUserService AsUser(int userId) => new CurrentUserService { UserId = userId };

    private static ICurrentUserService AsShareLink(int tripId, TripRole role) =>
        new CurrentUserService { ShareLinkTripId = tripId, ShareLinkRole = role };

    private TripAccessService Service(ICurrentUserService currentUser) => new(_context, currentUser);

    [Theory]
    [InlineData(TripRole.Viewer)]
    [InlineData(TripRole.Editor)]
    public async Task Allows_owner_at_any_required_role(TripRole requiredRole)
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        var service = Service(AsUser(owner.Id));

        Assert.True(await service.HasAccessAsync(trip.Id, requiredRole));
    }

    [Theory]
    [InlineData(TripRole.Viewer)]
    [InlineData(TripRole.Editor)]
    public async Task Rejects_unrelated_user_at_any_required_role(TripRole requiredRole)
    {
        var owner = NewUser("Owner");
        var stranger = NewUser("Stranger");
        _context.Users.AddRange(owner, stranger);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        var service = Service(AsUser(stranger.Id));

        Assert.False(await service.HasAccessAsync(trip.Id, requiredRole));
    }

    [Fact]
    public async Task Rejects_access_to_a_trip_that_does_not_exist()
    {
        var someone = NewUser("Someone");
        _context.Users.Add(someone);
        await _context.SaveChangesAsync();

        var service = Service(AsUser(someone.Id));

        Assert.False(await service.HasAccessAsync(999, TripRole.Viewer));
    }

    // Every stored-role x required-role combination for an accepted collaborator. This is the
    // exact matrix the original bug got backwards: a stored Editor was denied a Viewer-level
    // (read) check, and a stored Viewer was wrongly granted an Editor-level (write) check.
    [Theory]
    [InlineData(TripRole.Viewer, TripRole.Viewer, true)]
    [InlineData(TripRole.Viewer, TripRole.Editor, false)]
    [InlineData(TripRole.Editor, TripRole.Viewer, true)]
    [InlineData(TripRole.Editor, TripRole.Editor, true)]
    public async Task Accepted_collaborator_role_gates_access_correctly(
        TripRole storedRole, TripRole requiredRole, bool expectedAllowed)
    {
        var owner = NewUser("Owner");
        var collaborator = NewUser("Collaborator");
        _context.Users.AddRange(owner, collaborator);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.Add(new TripCollaborator
        {
            TripId = trip.Id,
            UserId = collaborator.Id,
            Role = storedRole,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var service = Service(AsUser(collaborator.Id));

        Assert.Equal(expectedAllowed, await service.HasAccessAsync(trip.Id, requiredRole));
    }

    // A pending invite (AcceptedAt still null) must never grant access — regardless of what role
    // it was invited at, and even for the lowest possible bar (Viewer). This is a separate branch
    // from the role comparison itself (the query's "c.AcceptedAt != null" filter), but it sits
    // right next to that comparison and is just as easy to get backwards.
    [Theory]
    [InlineData(TripRole.Viewer)]
    [InlineData(TripRole.Editor)]
    public async Task Rejects_pending_invite_even_at_the_lowest_required_role(TripRole storedRole)
    {
        var owner = NewUser("Owner");
        var invitee = NewUser("Invitee");
        _context.Users.AddRange(owner, invitee);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.Add(new TripCollaborator
        {
            TripId = trip.Id,
            UserId = invitee.Id,
            Role = storedRole,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = null,
        });
        await _context.SaveChangesAsync();

        var service = Service(AsUser(invitee.Id));

        Assert.False(await service.HasAccessAsync(trip.Id, TripRole.Viewer));
    }

    // Every role x required-role combination for an anonymous share-link visitor — the other half
    // of ICurrentUserService that HasAccessAsync branches on. This path never touches the
    // database at all (the link was already validated by ShareLinkAuthHandler), so it isn't
    // exposed to the storage-level bug, but it's the same method and the same "does this role
    // satisfy the minimum" question, so it belongs in the same matrix.
    [Theory]
    [InlineData(TripRole.Viewer, TripRole.Viewer, true)]
    [InlineData(TripRole.Viewer, TripRole.Editor, false)]
    [InlineData(TripRole.Editor, TripRole.Viewer, true)]
    [InlineData(TripRole.Editor, TripRole.Editor, true)]
    public async Task Share_link_role_gates_access_correctly(
        TripRole linkRole, TripRole requiredRole, bool expectedAllowed)
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();

        var service = Service(AsShareLink(trip.Id, linkRole));

        Assert.Equal(expectedAllowed, await service.HasAccessAsync(trip.Id, requiredRole));
    }

    [Fact]
    public async Task Rejects_share_link_scoped_to_a_different_trip()
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        var otherTrip = NewTrip(owner);
        _context.Trips.AddRange(trip, otherTrip);
        await _context.SaveChangesAsync();

        // A valid Editor share link — but scoped to otherTrip, not trip.
        var service = Service(AsShareLink(otherTrip.Id, TripRole.Editor));

        Assert.False(await service.HasAccessAsync(trip.Id, TripRole.Viewer));
    }
}
