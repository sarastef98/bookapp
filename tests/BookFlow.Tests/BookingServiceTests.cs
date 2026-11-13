using BookFlow.Api.Data;
using BookFlow.Api.Domain;
using BookFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Tests;

public sealed class BookingServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAllAsync_ReturnsBookingsOrderedById()
    {
        await using var database = await CreateDatabaseAsync();
        var first = await CreateValidBookingAsync(database, "Alice");
        var second = await SeedBookingAsync(database, Now.AddHours(3), Now.AddHours(4), BookingStatus.Confirmed);

        var bookings = await database.Service.GetAllAsync(CancellationToken.None);

        Assert.Collection(
            bookings,
            booking => Assert.Equal(first.Id, booking.Id),
            booking => Assert.Equal(second.Id, booking.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBookingWhenFound()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await CreateValidBookingAsync(database, "Alice");

        var booking = await database.Service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.NotNull(booking);
        Assert.Equal(database.ResourceId, booking.ResourceId);
        Assert.Equal("Alice", booking.CustomerName);
        Assert.Equal(Now.AddHours(1), booking.Start);
        Assert.Equal(Now.AddHours(2), booking.End);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        await using var database = await CreateDatabaseAsync();

        var booking = await database.Service.GetByIdAsync(42, CancellationToken.None);

        Assert.Null(booking);
    }

    [Fact]
    public async Task CreateAsync_PersistsBookingAndAssignsId()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", Now.AddHours(1), Now.AddHours(2), CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Booking);
        Assert.True(result.Booking.Id > 0);
        Assert.Equal(result.Booking.Id, await database.Context.Bookings.Select(item => item.Id).SingleAsync());
    }

    [Fact]
    public async Task CreateAsync_NormalizesTimesToUtc()
    {
        await using var database = await CreateDatabaseAsync();
        var offset = TimeSpan.FromHours(2);
        var start = Now.AddHours(3).ToOffset(offset);
        var end = Now.AddHours(4).ToOffset(offset);

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", start, end, CancellationToken.None);

        Assert.NotNull(result.Booking);
        Assert.Equal(TimeSpan.Zero, result.Booking.Start.Offset);
        Assert.Equal(TimeSpan.Zero, result.Booking.End.Offset);
        Assert.Equal(start, result.Booking.Start);
    }

    [Fact]
    public async Task CreateAsync_FailsWhenEndEqualsStart()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", Now.AddHours(1), Now.AddHours(1), CancellationToken.None);

        Assert.Equal(BookingError.EndNotAfterStart, result.Error);
        Assert.Empty(database.Context.Bookings);
    }

    [Fact]
    public async Task CreateAsync_FailsWhenEndBeforeStart()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", Now.AddHours(2), Now.AddHours(1), CancellationToken.None);

        Assert.Equal(BookingError.EndNotAfterStart, result.Error);
    }

    [Fact]
    public async Task CreateAsync_FailsWhenStartInPast()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", Now.AddMinutes(-1), Now.AddHours(1), CancellationToken.None);

        Assert.Equal(BookingError.StartInPast, result.Error);
    }

    [Fact]
    public async Task CreateAsync_AllowsStartExactlyNow()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Alice", Now, Now.AddHours(1), CancellationToken.None);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreateAsync_FailsWhenResourceDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.CreateAsync(
            999, "Alice", Now.AddHours(1), Now.AddHours(2), CancellationToken.None);

        Assert.Equal(BookingError.ResourceNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingBooking()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await CreateValidBookingAsync(database, "Alice");

        var result = await database.Service.UpdateAsync(
            created.Id, database.ResourceId, "Bob", Now.AddHours(5), Now.AddHours(6), CancellationToken.None);

        Assert.Null(result.Error);
        var stored = await database.Service.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Bob", stored.CustomerName);
        Assert.Equal(Now.AddHours(5), stored.Start);
        Assert.Equal(Now.AddHours(6), stored.End);
    }

    [Fact]
    public async Task UpdateAsync_FailsWhenBookingDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();

        var result = await database.Service.UpdateAsync(
            42, database.ResourceId, "Bob", Now.AddHours(1), Now.AddHours(2), CancellationToken.None);

        Assert.Equal(BookingError.BookingNotFound, result.Error);
    }

    [Fact]
    public async Task UpdateAsync_FailsValidationAndLeavesBookingUnchanged()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await CreateValidBookingAsync(database, "Alice");

        var result = await database.Service.UpdateAsync(
            created.Id, database.ResourceId, "Bob", Now.AddHours(3), Now.AddHours(2), CancellationToken.None);

        Assert.Equal(BookingError.EndNotAfterStart, result.Error);
        var stored = await database.Service.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Alice", stored.CustomerName);
    }

    [Fact]
    public async Task UpdateAsync_FailsWhenResourceDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await CreateValidBookingAsync(database, "Alice");

        var result = await database.Service.UpdateAsync(
            created.Id, 999, "Alice", Now.AddHours(1), Now.AddHours(2), CancellationToken.None);

        Assert.Equal(BookingError.ResourceNotFound, result.Error);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingBooking()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await CreateValidBookingAsync(database, "Alice");

        var deleted = await database.Service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await database.Service.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenBookingDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();

        var deleted = await database.Service.DeleteAsync(42, CancellationToken.None);

        Assert.False(deleted);
    }

    // Existing confirmed booking for overlap tests: [Now+10h, Now+12h).
    private static readonly DateTimeOffset ExistingStart = Now.AddHours(10);
    private static readonly DateTimeOffset ExistingEnd = Now.AddHours(12);

    public static TheoryData<int, int> OverlappingRangesInMinutes => new()
    {
        { 0, 120 },     // identical
        { 30, 90 },     // fully contained in existing
        { -30, 150 },   // fully contains existing
        { -60, 1 },     // overlaps start by one minute
        { 119, 180 },   // overlaps end by one minute
        { 0, 60 },      // same start, shorter
        { 60, 120 }     // same end, shorter
    };

    public static TheoryData<int, int> NonOverlappingRangesInMinutes => new()
    {
        { -60, 0 },     // touches existing start
        { 120, 180 },   // touches existing end
        { -120, -60 },  // entirely before
        { 180, 240 }    // entirely after
    };

    [Theory]
    [MemberData(nameof(OverlappingRangesInMinutes))]
    public async Task CreateAsync_FailsWhenOverlappingConfirmedBooking(int startOffset, int endOffset)
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var result = await database.Service.CreateAsync(
            database.ResourceId,
            "Bob",
            ExistingStart.AddMinutes(startOffset),
            ExistingStart.AddMinutes(endOffset),
            CancellationToken.None);

        Assert.Equal(BookingError.Overlap, result.Error);
        Assert.Equal(1, await database.Context.Bookings.CountAsync());
    }

    [Theory]
    [MemberData(nameof(NonOverlappingRangesInMinutes))]
    public async Task CreateAsync_AllowsNonOverlappingOrTouchingRanges(int startOffset, int endOffset)
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var result = await database.Service.CreateAsync(
            database.ResourceId,
            "Bob",
            ExistingStart.AddMinutes(startOffset),
            ExistingStart.AddMinutes(endOffset),
            CancellationToken.None);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreateAsync_DetectsOverlapAcrossTimeZoneOffsets()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);
        var offset = TimeSpan.FromHours(-5);

        var result = await database.Service.CreateAsync(
            database.ResourceId,
            "Bob",
            ExistingStart.AddMinutes(30).ToOffset(offset),
            ExistingEnd.AddMinutes(30).ToOffset(offset),
            CancellationToken.None);

        Assert.Equal(BookingError.Overlap, result.Error);
    }

    [Fact]
    public async Task CreateAsync_IgnoresCancelledBookings()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Cancelled);

        var result = await database.Service.CreateAsync(
            database.ResourceId, "Bob", ExistingStart, ExistingEnd, CancellationToken.None);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CreateAsync_AllowsSamePeriodOnDifferentResource()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);
        var otherResource = await new ResourceService(database.Context)
            .CreateAsync("Board room", 4, CancellationToken.None);

        var result = await database.Service.CreateAsync(
            otherResource.Id, "Bob", ExistingStart, ExistingEnd, CancellationToken.None);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task UpdateAsync_AllowsOverlapWithItself()
    {
        await using var database = await CreateDatabaseAsync();
        var existing = await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);

        var result = await database.Service.UpdateAsync(
            existing.Id,
            database.ResourceId,
            "Alice",
            ExistingStart.AddMinutes(30),
            ExistingEnd.AddMinutes(30),
            CancellationToken.None);

        Assert.Null(result.Error);
    }

    [Fact]
    public async Task UpdateAsync_FailsWhenMovedIntoAnotherConfirmedBooking()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Confirmed);
        var other = await SeedBookingAsync(database, Now.AddHours(20), Now.AddHours(21), BookingStatus.Confirmed);

        var result = await database.Service.UpdateAsync(
            other.Id,
            database.ResourceId,
            "Bob",
            ExistingStart.AddMinutes(30),
            ExistingStart.AddMinutes(90),
            CancellationToken.None);

        Assert.Equal(BookingError.Overlap, result.Error);
        var stored = await database.Service.GetByIdAsync(other.Id, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(Now.AddHours(20), stored.Start);
    }

    [Fact]
    public async Task UpdateAsync_AllowsMoveIntoCancelledBookingSlot()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedBookingAsync(database, ExistingStart, ExistingEnd, BookingStatus.Cancelled);
        var other = await SeedBookingAsync(database, Now.AddHours(20), Now.AddHours(21), BookingStatus.Confirmed);

        var result = await database.Service.UpdateAsync(
            other.Id, database.ResourceId, "Bob", ExistingStart, ExistingEnd, CancellationToken.None);

        Assert.Null(result.Error);
    }

    public static TheoryData<int> ConcurrencyIterations => new(Enumerable.Range(1, 10));

    // Two independent contexts (separate connections to the same SQLite file) race to book the
    // same slot. The Serializable/BEGIN IMMEDIATE transaction must let exactly one of them win.
    [Theory]
    [MemberData(nameof(ConcurrencyIterations))]
    public async Task CreateAsync_ConcurrentRequestsForSamePeriod_OnlyOneSucceeds(int iteration)
    {
        _ = iteration;
        var path = Path.Combine(Path.GetTempPath(), $"bookflow-tests-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        BookFlowDbContext? first = null;
        BookFlowDbContext? second = null;

        try
        {
            int resourceId;
            await using (var setup = CreateFileContext(connectionString))
            {
                await setup.Database.EnsureCreatedAsync();
                var resource = await new ResourceService(setup).CreateAsync("Meeting room", 8, CancellationToken.None);
                resourceId = resource.Id;
            }

            first = CreateFileContext(connectionString);
            second = CreateFileContext(connectionString);
            var firstService = new BookingService(first, new FixedTimeProvider(Now));
            var secondService = new BookingService(second, new FixedTimeProvider(Now));

            using var barrier = new Barrier(2);
            Task<BookingResult> RunAsync(BookingService service, string customerName) => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await service.CreateAsync(
                    resourceId, customerName, ExistingStart, ExistingEnd, CancellationToken.None);
            });

            var results = await Task.WhenAll(RunAsync(firstService, "Alice"), RunAsync(secondService, "Bob"));

            Assert.Single(results, result => result.Error is null && result.Booking is not null);
            Assert.Single(results, result => result.Error == BookingError.Overlap);

            await using var verify = CreateFileContext(connectionString);
            Assert.Equal(1, await verify.Bookings.CountAsync());
        }
        finally
        {
            if (first is not null)
            {
                await first.DisposeAsync();
            }

            if (second is not null)
            {
                await second.DisposeAsync();
            }

            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    [Fact]
    public async Task CreateAsync_ReturnsBusyWhenAnotherConnectionHoldsWriteLock()
    {
        await using var database = await TempFileDatabase.CreateAsync();
        await using var context = database.CreateContext(timeoutSeconds: 1);
        var service = new BookingService(context, new FixedTimeProvider(Now));

        await using (await database.HoldWriteLockAsync())
        {
            var result = await service.CreateAsync(
                database.ResourceId, "Alice", ExistingStart, ExistingEnd, CancellationToken.None);

            Assert.Equal(BookingError.Busy, result.Error);
            Assert.Null(result.Booking);
        }

        await using var verify = database.CreateContext(timeoutSeconds: 1);
        Assert.Equal(0, await verify.Bookings.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsBusyWhenAnotherConnectionHoldsWriteLock()
    {
        await using var database = await TempFileDatabase.CreateAsync();
        int bookingId;
        await using (var seed = database.CreateContext(timeoutSeconds: 1))
        {
            var created = await new BookingService(seed, new FixedTimeProvider(Now)).CreateAsync(
                database.ResourceId, "Alice", ExistingStart, ExistingEnd, CancellationToken.None);
            Assert.NotNull(created.Booking);
            bookingId = created.Booking.Id;
        }

        await using var context = database.CreateContext(timeoutSeconds: 1);
        var service = new BookingService(context, new FixedTimeProvider(Now));

        await using (await database.HoldWriteLockAsync())
        {
            var result = await service.UpdateAsync(
                bookingId, database.ResourceId, "Bob", Now.AddHours(20), Now.AddHours(21), CancellationToken.None);

            Assert.Equal(BookingError.Busy, result.Error);
            Assert.Null(result.Booking);
        }

        await using var verify = database.CreateContext(timeoutSeconds: 1);
        var stored = await verify.Bookings.SingleAsync();
        Assert.Equal(bookingId, stored.Id);
        Assert.Equal("Alice", stored.CustomerName);
        Assert.Equal(ExistingStart, stored.Start);
        Assert.Equal(ExistingEnd, stored.End);
    }

    public static TheoryData<int, int> InvalidPeriodsInMinutes => new()
    {
        { 0, 0 },     // End == Start
        { 60, 0 }     // End < Start
    };

    // The busy timeout is deliberately long (5 s). If the period were validated after taking the
    // write lock, these calls would block for the full timeout and then return Busy.
    [Theory]
    [MemberData(nameof(InvalidPeriodsInMinutes))]
    public async Task CreateAsync_RejectsInvalidPeriodWithoutWaitingForWriteLock(int startOffset, int endOffset)
    {
        await using var database = await TempFileDatabase.CreateAsync();
        await using var context = database.CreateContext(timeoutSeconds: 5);
        var service = new BookingService(context, new FixedTimeProvider(Now));

        await using (await database.HoldWriteLockAsync())
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await service.CreateAsync(
                database.ResourceId,
                "Alice",
                ExistingStart.AddMinutes(startOffset),
                ExistingStart.AddMinutes(endOffset),
                CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(BookingError.EndNotAfterStart, result.Error);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Expected immediate rejection, took {stopwatch.Elapsed}.");
        }
    }

    [Theory]
    [MemberData(nameof(InvalidPeriodsInMinutes))]
    public async Task UpdateAsync_RejectsInvalidPeriodWithoutWaitingForWriteLock(int startOffset, int endOffset)
    {
        await using var database = await TempFileDatabase.CreateAsync();
        await using var context = database.CreateContext(timeoutSeconds: 5);
        var service = new BookingService(context, new FixedTimeProvider(Now));

        await using (await database.HoldWriteLockAsync())
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await service.UpdateAsync(
                1,
                database.ResourceId,
                "Alice",
                ExistingStart.AddMinutes(startOffset),
                ExistingStart.AddMinutes(endOffset),
                CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(BookingError.EndNotAfterStart, result.Error);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Expected immediate rejection, took {stopwatch.Elapsed}.");
        }
    }

    // Temporary SQLite file database with one seeded resource, for tests that need several
    // connections (locking/concurrency). Deletes the file and its journal on dispose.
    private sealed class TempFileDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private TempFileDatabase(string path)
        {
            _path = path;
        }

        public int ResourceId { get; private set; }

        public static async Task<TempFileDatabase> CreateAsync()
        {
            var database = new TempFileDatabase(
                Path.Combine(Path.GetTempPath(), $"bookflow-tests-{Guid.NewGuid():N}.db"));
            await using var setup = database.CreateContext(timeoutSeconds: 5);
            await setup.Database.EnsureCreatedAsync();
            var resource = await new ResourceService(setup).CreateAsync("Meeting room", 8, CancellationToken.None);
            database.ResourceId = resource.Id;
            return database;
        }

        public BookFlowDbContext CreateContext(int timeoutSeconds)
        {
            var options = new DbContextOptionsBuilder<BookFlowDbContext>()
                .UseSqlite(ConnectionString(timeoutSeconds), sqlite => sqlite.CommandTimeout(timeoutSeconds))
                .Options;
            return new BookFlowDbContext(options);
        }

        // Opens a separate connection and takes the database write lock until disposed (ROLLBACK).
        public async Task<IAsyncDisposable> HoldWriteLockAsync()
        {
            var connection = new SqliteConnection(ConnectionString(timeoutSeconds: 5));
            await connection.OpenAsync();
            await using (var begin = connection.CreateCommand())
            {
                begin.CommandText = "BEGIN IMMEDIATE;";
                await begin.ExecuteNonQueryAsync();
            }

            return new WriteLock(connection);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-journal", _path + "-wal", _path + "-shm" })
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            return ValueTask.CompletedTask;
        }

        private string ConnectionString(int timeoutSeconds)
        {
            return new SqliteConnectionStringBuilder
            {
                DataSource = _path,
                DefaultTimeout = timeoutSeconds
            }.ToString();
        }

        private sealed class WriteLock(SqliteConnection connection) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await using (var rollback = connection.CreateCommand())
                {
                    rollback.CommandText = "ROLLBACK;";
                    await rollback.ExecuteNonQueryAsync();
                }

                await connection.DisposeAsync();
            }
        }
    }

    private static BookFlowDbContext CreateFileContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BookFlowDbContext>()
            .UseSqlite(connectionString)
            .Options;
        return new BookFlowDbContext(options);
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

    private static async Task<Booking> CreateValidBookingAsync(TestDatabase database, string customerName)
    {
        var result = await database.Service.CreateAsync(
            database.ResourceId, customerName, Now.AddHours(1), Now.AddHours(2), CancellationToken.None);
        Assert.NotNull(result.Booking);
        return result.Booking;
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

        return new TestDatabase(connection, context, service, resource.Id);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestDatabase(
        SqliteConnection connection,
        BookFlowDbContext context,
        BookingService service,
        int resourceId) : IAsyncDisposable
    {
        public BookFlowDbContext Context { get; } = context;

        public BookingService Service { get; } = service;

        public int ResourceId { get; } = resourceId;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
