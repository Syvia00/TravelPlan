using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TravelPlan.Api.Controllers;
using TravelPlan.Api.Data;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Api.Services.Reports;
using TravelPlan.Shared.DTOs.Trips;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Tests;

/// <summary>
/// Covers Session 7.5's "the database cascade doesn't reach Blob Storage" requirement: deleting a
/// whole Trip must clean up every one of its plan items' photos in one go (a single
/// {tripId}/ prefix delete — see IPhotoStorageService.DeleteAllForTripAsync), not by enumerating
/// plan items first. ITripCompletionService/ITripReportService are never touched by Delete, so
/// they're stubbed to throw if that ever stops being true.
/// </summary>
public class TripsControllerPhotoCleanupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;
    private readonly FakePhotoStorageService _photoStorage = new();

    public TripsControllerPhotoCleanupTests()
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

    private TripsController Controller(int userId)
    {
        var currentUser = new CurrentUserService { UserId = userId };
        return new TripsController(
            new TripRepository(_context),
            currentUser,
            new TripAccessService(_context, currentUser),
            new NotUsedTripCompletionService(),
            new NotUsedTripReportService(),
            _photoStorage,
            new CreateTripDtoValidator(),
            new UpdateTripDtoValidator(),
            NullLogger<TripsController>.Instance);
    }

    [Fact]
    public async Task Deleting_a_trip_deletes_all_its_plan_items_blob_photos()
    {
        var owner = NewUser();
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var itemA = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        var itemB = new PlanItem { Trip = trip, Title = "Beach", SortOrder = 1 };
        _context.PlanItems.AddRange(itemA, itemB);
        await _context.SaveChangesAsync();

        var photoA = await _photoStorage.UploadAsync(trip.Id, itemA.Id, new MemoryStream(new byte[10]), "image/jpeg", CancellationToken.None);
        await _photoStorage.UploadThumbnailAsync(trip.Id, itemA.Id, photoA.Name, new byte[5], CancellationToken.None);
        var photoB = await _photoStorage.UploadAsync(trip.Id, itemB.Id, new MemoryStream(new byte[10]), "image/png", CancellationToken.None);
        await _photoStorage.UploadThumbnailAsync(trip.Id, itemB.Id, photoB.Name, new byte[5], CancellationToken.None);

        var result = await Controller(owner.Id).Delete(trip.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Contains(trip.Id, _photoStorage.DeletedForTripCalls);
        Assert.Empty(await _photoStorage.ListAsync(trip.Id, itemA.Id, CancellationToken.None));
        Assert.Empty(await _photoStorage.ListAsync(trip.Id, itemB.Id, CancellationToken.None));
        Assert.False(_photoStorage.HasThumbnail(photoA.Name));
        Assert.False(_photoStorage.HasThumbnail(photoB.Name));
    }

    private sealed class NotUsedTripCompletionService : ITripCompletionService
    {
        public Task<Trip?> CompleteTripAsync(int tripId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("TripsController.Delete must not touch ITripCompletionService.");

        public Task<int> CompleteDueTripsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("TripsController.Delete must not touch ITripCompletionService.");
    }

    private sealed class NotUsedTripReportService : ITripReportService
    {
        public Task<TripMemory?> GenerateReportAsync(int tripId, TripMemoryReportType reportType, string? title, string? description, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("TripsController.Delete must not touch ITripReportService.");

        public Task<TripMemory> GenerateReportForTripAsync(Trip trip, TripMemoryReportType reportType, string? title, string? description, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("TripsController.Delete must not touch ITripReportService.");
    }
}
