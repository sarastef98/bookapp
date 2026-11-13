using BookFlow.Api.Contracts;
using BookFlow.Api.Domain;
using BookFlow.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ResourcesController(IResourceService resourceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResourceDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var resources = await resourceService.GetAllAsync(cancellationToken);
        return Ok(resources.Select(ToDto).ToArray());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResourceDto>> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var resource = await resourceService.GetByIdAsync(id, cancellationToken);
        return resource is null ? NotFound() : Ok(ToDto(resource));
    }

    [HttpPost]
    public async Task<ActionResult<ResourceDto>> CreateAsync(
        CreateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var resource = await resourceService.CreateAsync(request.Name, request.Capacity, cancellationToken);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = resource.Id }, ToDto(resource));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await resourceService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static ResourceDto ToDto(Resource resource)
    {
        return new ResourceDto(resource.Id, resource.Name, resource.Capacity);
    }
}
