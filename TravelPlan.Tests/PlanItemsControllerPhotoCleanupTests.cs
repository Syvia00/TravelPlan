using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelPlan.Api.Controllers;
using TravelPlan.Api.Data;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.PlanItems;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Tests;

/// <summary>
/// Covers Session 7.5's "the database cascade doesn't reach Blob Storage" requirement: deleting a
/// PlanItem must explicitly clean up its photos. Uses a real SQLite database for the PlanItem/Trip
/// data and FakePhotoStorageService to observe the cleanup call without a real Azure Storage account.
/// </summary>
public class PlanItemsControllerPhotoCleanupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;
    private readonly FakePhotoStorageService _photoStorage = new();

    public PlanItemsControllerPhotoCleanupTests()
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

    private static User NewUser() => new()
    {
        DisplayName = "Owner",
        ExternalAuthId = Guid.NewGuid().ToString(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

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

    private PlanItemsController Controller(int userId) =>
        new(
            new PlanItemRepository(_context),
            new TripAccessService(_context, new CurrentUserService { UserId = userId }),
            _photoStorage,
            new CreatePlanItemDtoValidator(),
            new UpdatePlanItemDtoValidator(),
            NullLogger<PlanItemsController>.Instance);

    [Fact]
    public async Task Deleting_a_plan_item_deletes_its_blob_photos()
    {
        var owner = NewUser();
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        await _photoStorage.UploadAsync(trip.Id, item.Id, new MemoryStream(new byte[10]), "image/jpeg", CancellationToken.None);

        var result = await Controller(owner.Id).Delete(item.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Contains((trip.Id, item.Id), _photoStorage.DeletedForPlanItemCalls);
        Assert.Empty(await _photoStorage.ListAsync(trip.Id, item.Id, CancellationToken.None));
    }
}
