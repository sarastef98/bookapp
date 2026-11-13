using BookFlow.Api.Data;
using BookFlow.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Api.Services;

public sealed class ResourceService(BookFlowDbContext dbContext) : IResourceService
{
    public async Task<IReadOnlyList<Resource>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Resources
            .AsNoTracking()
            .OrderBy(resource => resource.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Resource?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(resource => resource.Id == id, cancellationToken);
    }

    public async Task<Resource> CreateAsync(string name, int capacity, CancellationToken cancellationToken)
    {
        var resource = new Resource
        {
            Name = name,
            Capacity = capacity
        };

        dbContext.Resources.Add(resource);
        await dbContext.SaveChangesAsync(cancellationToken);
        return resource;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .FirstOrDefaultAsync(resource => resource.Id == id, cancellationToken);

        if (resource is null)
        {
            return false;
        }

        dbContext.Resources.Remove(resource);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
