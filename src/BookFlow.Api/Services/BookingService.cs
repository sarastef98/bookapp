using System.Data;
using BookFlow.Api.Data;
using BookFlow.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Api.Services;

public sealed class BookingService(BookFlowDbContext dbContext, TimeProvider timeProvider) : IBookingService
{
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    public async Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .OrderBy(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(booking => booking.Id == id, cancellationToken);
    }

    public async Task<BookingResult> CreateAsync(
        int resourceId,
        string customerName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        start = start.ToUniversalTime();
        end = end.ToUniversalTime();

        var periodError = ValidatePeriod(start, end);
        if (periodError is not null)
        {
            return BookingResult.Failure(periodError.Value);
        }

        return await RunInWriteTransactionAsync(async () =>
        {
            var error = await ValidateResourceAndOverlapAsync(
                resourceId, start, end, excludeBookingId: null, cancellationToken);
            if (error is not null)
            {
                return BookingResult.Failure(error.Value);
            }

            var booking = new Booking
            {
                ResourceId = resourceId,
                CustomerName = customerName,
                Start = start,
                End = end
            };

            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);
            return BookingResult.Success(booking);
        }, cancellationToken);
    }

    public async Task<BookingResult> UpdateAsync(
        int id,
        int resourceId,
        string customerName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        start = start.ToUniversalTime();
        end = end.ToUniversalTime();

        var periodError = ValidatePeriod(start, end);
        if (periodError is not null)
        {
            return BookingResult.Failure(periodError.Value);
        }

        return await RunInWriteTransactionAsync(async () =>
        {
            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (booking is null)
            {
                return BookingResult.Failure(BookingError.BookingNotFound);
            }

            var error = await ValidateResourceAndOverlapAsync(
                resourceId, start, end, excludeBookingId: id, cancellationToken);
            if (error is not null)
            {
                return BookingResult.Failure(error.Value);
            }

            booking.ResourceId = resourceId;
            booking.CustomerName = customerName;
            booking.Start = start;
            booking.End = end;

            await dbContext.SaveChangesAsync(cancellationToken);
            return BookingResult.Success(booking);
        }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (booking is null)
        {
            return false;
        }

        dbContext.Bookings.Remove(booking);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // The overlap check and the write must be atomic, otherwise two concurrent requests can
    // both pass the check and insert overlapping bookings. On SQLite a Serializable transaction
    // starts as BEGIN IMMEDIATE, taking the write lock before the check so writers serialize.
    // If the lock cannot be acquired within the command timeout, the request fails with Busy
    // instead of surfacing a raw exception.
    // On other providers (e.g. SQL Server) Serializable also prevents the race but can deadlock;
    // a provider switch needs deadlock handling and, with a retrying execution strategy, must wrap
    // this unit of work in Database.CreateExecutionStrategy().ExecuteAsync(...).
    private async Task<BookingResult> RunInWriteTransactionAsync(
        Func<Task<BookingResult>> work,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var result = await work();
            if (result.Error is null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (Exception exception) when (IsDatabaseBusy(exception))
        {
            dbContext.ChangeTracker.Clear();
            return BookingResult.Failure(BookingError.Busy);
        }
    }

    private static bool IsDatabaseBusy(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqliteException { SqliteErrorCode: SqliteBusy or SqliteLocked })
            {
                return true;
            }
        }

        return false;
    }

    // Pure input checks: run before taking the write lock so invalid requests never hold it.
    private BookingError? ValidatePeriod(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return BookingError.EndNotAfterStart;
        }

        if (start < timeProvider.GetUtcNow())
        {
            return BookingError.StartInPast;
        }

        return null;
    }

    private async Task<BookingError?> ValidateResourceAndOverlapAsync(
        int resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        var resourceExists = await dbContext.Resources
            .AnyAsync(resource => resource.Id == resourceId, cancellationToken);

        if (!resourceExists)
        {
            return BookingError.ResourceNotFound;
        }

        return await HasOverlapAsync(resourceId, start, end, excludeBookingId, cancellationToken)
            ? BookingError.Overlap
            : null;
    }

    // Half-open intervals [Start, End): ranges that merely touch (one ends exactly when
    // the next starts) do not overlap. Cancelled bookings never block a slot.
    private async Task<bool> HasOverlapAsync(
        int resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.ResourceId == resourceId
                && booking.Status == BookingStatus.Confirmed
                && booking.Id != excludeBookingId)
            .AnyAsync(booking => booking.Start < end && booking.End > start, cancellationToken);
    }
}
