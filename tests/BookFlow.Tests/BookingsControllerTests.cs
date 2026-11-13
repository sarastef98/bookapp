using BookFlow.Api.Contracts;
using BookFlow.Api.Controllers;
using BookFlow.Api.Data;
using BookFlow.Api.Domain;
using BookFlow.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Tests;

public sealed class BookingsControllerTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Existing booking used by the tests: [Now+10h, Now+12h).
    private static readonly DateTimeOffset ExistingStart = Now.AddHours(10);
    private static readonly DateTimeOffset ExistingEnd = Now.AddHours(12);

    [Fact]
    public async Task CreateAsync_Returns409ProblemDetailsWhenOverlappingConfirmedBooking()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingStart.AddMinutes(30), ExistingEnd.AddMinutes(30)),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status409Conflict);
        Assert.Equal(1, await database.Context.Bookings.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_Returns400ProblemDetailsWhenEndEqualsStart()
    {
        await using var database = await CreateDatabaseAsync();

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingStart, ExistingStart),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest);
        Assert.Empty(database.Context.Bookings);
    }

    [Fact]
    public async Task CreateAsync_Returns400ProblemDetailsWhenEndBeforeStart()
    {
        await using var database = await CreateDatabaseAsync();

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingEnd, ExistingStart),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status400BadRequest);
        Assert.Empty(database.Context.Bookings);
    }

    [Fact]
    public async Task CreateAsync_Returns201WhenStartTouchesExistingEnd()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingEnd, ExistingEnd.AddHours(1)),
            CancellationToken.None);

        var dto = AssertCreated(response.Result);
        Assert.Equal(ExistingEnd, dto.Start);
        Assert.Equal(ExistingEnd.AddHours(1), dto.End);
    }

    [Fact]
    public async Task CreateAsync_Returns201WhenEndTouchesExistingStart()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingStart.AddHours(-1), ExistingStart),
            CancellationToken.None);

        var dto = AssertCreated(response.Result);
        Assert.Equal(ExistingStart.AddHours(-1), dto.Start);
        Assert.Equal(ExistingStart, dto.End);
    }

    [Fact]
    public async Task CreateAsync_Returns201WhenSlotOnlyHasCancelledBooking()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Cancelled);

        var response = await database.Controller.CreateAsync(
            CreateRequest(database, ExistingStart, ExistingEnd),
            CancellationToken.None);

        var dto = AssertCreated(response.Result);
        Assert.Equal(database.ResourceId, dto.ResourceId);
        Assert.Equal(nameof(BookingStatus.Confirmed), dto.Status);
    }

    [Fact]
    public async Task UpdateAsync_Returns409ProblemDetailsWhenMovedOntoAnotherConfirmedBooking()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);
        var other = await SeedBookingAsync(database, Now.AddHours(20), Now.AddHours(21), BookingStatus.Confirmed);

        var response = await database.Controller.UpdateAsync(
            other.Id,
            new UpdateBookingRequest(database.ResourceId, "Bob", ExistingStart, ExistingEnd),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status409Conflict);
        var stored = await database.Context.Bookings.AsNoTracking().SingleAsync(item => item.Id == other.Id);
        Assert.Equal(Now.AddHours(20), stored.Start);
        Assert.Equal(Now.AddHours(21), stored.End);
    }

    [Fact]
    public async Task CreateAsync_Returns503ProblemDetailsWithRetryAfterWhenServiceIsBusy()
    {
        var controller = CreateController(new BusyBookingService());

        var response = await controller.CreateAsync(
            new CreateBookingRequest(1, "Bob", ExistingStart, ExistingEnd),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status503ServiceUnavailable);
        Assert.Equal("1", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task UpdateAsync_Returns503ProblemDetailsWithRetryAfterWhenServiceIsBusy()
    {
        var controller = CreateController(new BusyBookingService());

        var response = await controller.UpdateAsync(
            1,
            new UpdateBookingRequest(1, "Bob", ExistingStart, ExistingEnd),
            CancellationToken.None);

        AssertProblem(response.Result, StatusCodes.Status503ServiceUnavailable);
        Assert.Equal("1", controller.Response.Headers.RetryAfter.ToString());
    }

    private static void AssertProblem(ActionResult? result, int expectedStatus)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
    }

    private static BookingDto AssertCreated(ActionResult? result)
    {
        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var dto = Assert.IsType<BookingDto>(created.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal(dto.Id, created.RouteValues?["id"]);
        return dto;
    }

    private static CreateBookingRequest CreateRequest(
        TestDatabase database,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        return new CreateBookingRequest(database.ResourceId, "Bob", start, end);
    }

    private static async Task<Booking> SeedBookingAsync(
        TestDatabase database,
        DateTimeOffset start,
        DateTimeOffset end,
        BookingStatus status)
    {
        var booking = new Booking
        {
            ResourceId = database.ResourceId,
            CustomerName = "Alice",
            Start = start,
            End = end,
            Status = status
        };
        database.Context.Bookings.Add(booking);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        return booking;
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookFlowDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new BookFlowDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var resource = await new ResourceService(context).CreateAsync("Meeting room", 8, CancellationToken.None);
        var service = new BookingService(context, new FixedTimeProvider(Now));
        var controller = CreateController(service);

        return new TestDatabase(connection, context, controller, resource.Id);
    }

    private static BookingsController CreateController(IBookingService service)
    {
        return new BookingsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    // Simulates the database write lock being unavailable.
    private sealed class BusyBookingService : IBookingService
    {
        public Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BookingResult> CreateAsync(
            int resourceId,
            string customerName,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken) =>
            Task.FromResult(BookingResult.Failure(BookingError.Busy));

        public Task<BookingResult> UpdateAsync(
            int id,
            int resourceId,
            string customerName,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken) =>
            Task.FromResult(BookingResult.Failure(BookingError.Busy));

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null)
        {
            return new ProblemDetails
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
        }

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null)
        {
            return new ValidationProblemDetails(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
        }
    }

    private sealed class TestDatabase(
        SqliteConnection connection,
        BookFlowDbContext context,
        BookingsController controller,
        int resourceId) : IAsyncDisposable
    {
        public BookFlowDbContext Context { get; } = context;

        public BookingsController Controller { get; } = controller;

        public int ResourceId { get; } = resourceId;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
