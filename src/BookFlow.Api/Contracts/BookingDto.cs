namespace BookFlow.Api.Contracts;

public sealed record BookingDto(
    int Id,
    int ResourceId,
    string CustomerName,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Status);
