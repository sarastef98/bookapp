using BookFlow.Api.Data;
using BookFlow.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Api.Services;

public sealed class BookingService(BookFlowDbContext dbContext, TimeProvider timeProvider) : IBookingService
{
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

        var error = await ValidateAsync(resourceId, start, end, excludeBookingId: null, cancellationToken);
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
    }

    public async Task<BookingResult> UpdateAsync(
        int id,
        int resourceId,
        string customerName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (booking is null)
        {
            return BookingResult.Failure(BookingError.BookingNotFound);
        }

        start = start.ToUniversalTime();
        end = end.ToUniversalTime();

        var error = await ValidateAsync(resourceId, start, end, excludeBookingId: id, cancellationToken);
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

    private async Task<BookingError?> ValidateAsync(
        int resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        int? excludeBookingId,
        CancellationToken cancellationToken)
    {
        if (end <= start)
        {
            return BookingError.EndNotAfterStart;
        }

        if (start < timeProvider.GetUtcNow())
        {
            return BookingError.StartInPast;
        }

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
