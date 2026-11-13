using BookFlow.Api.Domain;

namespace BookFlow.Api.Services;

public interface IBookingService
{
    Task<IReadOnlyList<Booking>> GetAllAsync(CancellationToken cancellationToken);

    Task<Booking?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<BookingResult> CreateAsync(
        int resourceId,
        string customerName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken);

    Task<BookingResult> UpdateAsync(
        int id,
        int resourceId,
        string customerName,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
