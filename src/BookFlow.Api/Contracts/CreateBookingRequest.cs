using System.ComponentModel.DataAnnotations;

namespace BookFlow.Api.Contracts;

public sealed record CreateBookingRequest(
    [property: Range(1, int.MaxValue)] int ResourceId,
    [property: Required, StringLength(200)] string CustomerName,
    [property: Required] DateTimeOffset? Start,
    [property: Required] DateTimeOffset? End);
