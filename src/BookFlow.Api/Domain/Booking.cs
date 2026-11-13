namespace BookFlow.Api.Domain;

public sealed class Booking
{
    public int Id { get; set; }

    public int ResourceId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
}
