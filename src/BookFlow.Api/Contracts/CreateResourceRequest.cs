using System.ComponentModel.DataAnnotations;

namespace BookFlow.Api.Contracts;

public sealed record CreateResourceRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Range(1, int.MaxValue)] int Capacity);
