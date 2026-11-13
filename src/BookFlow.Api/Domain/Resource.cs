namespace BookFlow.Api.Domain;

public sealed class Resource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }
}
