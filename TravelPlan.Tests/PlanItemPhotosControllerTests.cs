using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Controllers;
using TravelPlan.Api.Data;
using TravelPlan.Api.Repositories;
using TravelPlan.Api.Services;
using TravelPlan.Shared.DTOs.PlanItems;
using TravelPlan.Shared.Models;
using TravelPlan.Shared.Models.Enums;

namespace TravelPlan.Tests;

/// <summary>
/// Exercises PlanItemPhotosController against a real SQLite database (for the PlanItem/Trip/access
/// data) and FakePhotoStorageService (standing in for Blob Storage — see that class). Covers the
/// validation and access-control rules from Session 7.5: non-image rejected, oversize rejected,
/// and Viewer denied upload/delete but allowed to view.
/// </summary>
public class PlanItemPhotosControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TravelPlanSqliteDbContext _context;
    private readonly FakePhotoStorageService _photoStorage = new();

    public PlanItemPhotosControllerTests()
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

    private static User NewUser(string displayName) => new()
    {
        DisplayName = displayName,
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

    private PlanItemPhotosController Controller(int userId) =>
        new(new PlanItemRepository(_context), new TripAccessService(_context, new CurrentUserService { UserId = userId }), _photoStorage);

    private static TestFormFile Jpeg(int sizeBytes = 100) => new(new byte[sizeBytes], "image/jpeg");

    [Fact]
    public async Task Upload_rejects_non_image_content_type()
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        var controller = Controller(owner.Id);
        var result = await controller.Upload(item.Id, new TestFormFile(new byte[100], "text/plain"), CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("file"));
    }

    [Fact]
    public async Task Upload_rejects_oversize_file()
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        var oversizeFile = Jpeg((int)PhotoContentTypes.MaxSizeBytes + 1);

        var controller = Controller(owner.Id);
        var result = await controller.Upload(item.Id, oversizeFile, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("file"));
    }

    [Fact]
    public async Task Upload_succeeds_for_owner_and_photo_appears_in_list()
    {
        var owner = NewUser("Owner");
        _context.Users.Add(owner);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        var controller = Controller(owner.Id);
        var uploadResult = await controller.Upload(item.Id, Jpeg(), CancellationToken.None);
        var uploaded = Assert.IsType<OkObjectResult>(uploadResult.Result).Value as PlanItemPhotoDto;
        Assert.NotNull(uploaded);

        var listResult = await controller.List(item.Id, CancellationToken.None);
        var photos = Assert.IsType<OkObjectResult>(listResult.Result).Value as IEnumerable<PlanItemPhotoDto>;
        Assert.Contains(photos!, p => p.Name == uploaded!.Name);
    }

    [Theory]
    [InlineData(TripRole.Viewer)]
    [InlineData(TripRole.Editor)]
    public async Task List_allows_both_viewer_and_editor_collaborators(TripRole storedRole)
    {
        var owner = NewUser("Owner");
        var collaborator = NewUser("Collaborator");
        _context.Users.AddRange(owner, collaborator);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
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

        var result = await Controller(collaborator.Id).List(item.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Viewer_cannot_upload()
    {
        var owner = NewUser("Owner");
        var viewer = NewUser("Viewer");
        _context.Users.AddRange(owner, viewer);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.Add(new TripCollaborator
        {
            TripId = trip.Id,
            UserId = viewer.Id,
            Role = TripRole.Viewer,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var result = await Controller(viewer.Id).Upload(item.Id, Jpeg(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Viewer_cannot_delete()
    {
        var owner = NewUser("Owner");
        var viewer = NewUser("Viewer");
        _context.Users.AddRange(owner, viewer);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.Add(new TripCollaborator
        {
            TripId = trip.Id,
            UserId = viewer.Id,
            Role = TripRole.Viewer,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var ownerController = Controller(owner.Id);
        var uploadResult = await ownerController.Upload(item.Id, Jpeg(), CancellationToken.None);
        var uploaded = (PlanItemPhotoDto)Assert.IsType<OkObjectResult>(uploadResult.Result).Value!;

        var deleteResult = await Controller(viewer.Id).Delete(item.Id, uploaded.Name, CancellationToken.None);

        Assert.IsType<NotFoundResult>(deleteResult);
    }

    [Fact]
    public async Task Editor_collaborator_can_upload_and_delete()
    {
        var owner = NewUser("Owner");
        var editor = NewUser("Editor");
        _context.Users.AddRange(owner, editor);
        var trip = NewTrip(owner);
        _context.Trips.Add(trip);
        var item = new PlanItem { Trip = trip, Title = "Museum", SortOrder = 0 };
        _context.PlanItems.Add(item);
        await _context.SaveChangesAsync();

        _context.TripCollaborators.Add(new TripCollaborator
        {
            TripId = trip.Id,
            UserId = editor.Id,
            Role = TripRole.Editor,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var editorController = Controller(editor.Id);
        var uploadResult = await editorController.Upload(item.Id, Jpeg(), CancellationToken.None);
        var uploaded = (PlanItemPhotoDto)Assert.IsType<OkObjectResult>(uploadResult.Result).Value!;

        var deleteResult = await editorController.Delete(item.Id, uploaded.Name, CancellationToken.None);

        Assert.IsType<NoContentResult>(deleteResult);
    }

    /// <summary>Minimal IFormFile — exactly what the controller reads (Length, ContentType,
    /// OpenReadStream); the rest is never touched by the code under test.</summary>
    private sealed class TestFormFile : IFormFile
    {
        private readonly byte[] _content;

        public TestFormFile(byte[] content, string contentType)
        {
            _content = content;
            ContentType = contentType;
        }

        public string ContentType { get; }

        public string ContentDisposition => string.Empty;

        public IHeaderDictionary Headers { get; } = new HeaderDictionary();

        public long Length => _content.Length;

        public string Name => "file";

        public string FileName => "photo";

        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            target.WriteAsync(_content, 0, _content.Length, cancellationToken);

        public Stream OpenReadStream() => new MemoryStream(_content);
    }
}
