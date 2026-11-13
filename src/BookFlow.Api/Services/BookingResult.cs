using BookFlow.Api.Domain;

namespace BookFlow.Api.Services;

public enum BookingError
{
    BookingNotFound,
    ResourceNotFound,
    EndNotAfterStart,
    StartInPast,
    Overlap
}

public sealed record BookingResult(Booking? Booking, BookingError? Error)
{
    public static BookingResult Success(Booking booking) => new(booking, null);

    public static BookingResult Failure(BookingError error) => new(null, error);
}
