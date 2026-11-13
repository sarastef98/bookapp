using BookFlow.Api.Domain;

namespace BookFlow.Api.Services;

public interface IResourceService
{
    Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken);

    Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<Resource> CreateAsync(string name, int capacity, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
