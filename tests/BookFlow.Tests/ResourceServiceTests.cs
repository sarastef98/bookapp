using BookFlow.Api.Data;
using BookFlow.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Tests;

public sealed class ResourceServiceTests
{
    [Fact]
    public async Task GetAllAsync_ReturnsResourcesOrderedById()
    {
        await using var database = await CreateDatabaseAsync();
        var first = await database.Service.CreateAsync("Meeting room", 8, CancellationToken.None);
        var second = await database.Service.CreateAsync("Conference room", 20, CancellationToken.None);

        var resources = await database.Service.GetAllAsync(CancellationToken.None);

        Assert.Collection(
            resources,
            resource => Assert.Equal(first.Id, resource.Id),
            resource => Assert.Equal(second.Id, resource.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsResourceWhenFound()
    {
        await using var database = await CreateDatabaseAsync();
        var created = await database.Service.CreateAsync("Meeting room", 8, CancellationToken.None);

        var resource = await database.Service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.NotNull(resource);
        Assert.Equal(created.Id, resource.Id);
        Assert.Equal("Meeting room", resource.Name);
        Assert.Equal(8, resource.Capacity);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotFound()
    {
        await using var database = await CreateDatabaseAsync();

        var resource = await database.Service.GetByIdAsync(42, CancellationToken.None);

        Assert.Null(resource);
    }

    [Fact]
    public async Task CreateAsync_PersistsResourceAndAssignsId()
    {
        await using var database = await CreateDatabaseAsync();

        var resource = await database.Service.CreateAsync("Meeting room", 8, CancellationToken.None);

        Assert.True(resource.Id > 0);
        Assert.Equal("Meeting room", resource.Name);
        Assert.Equal(8, resource.Capacity);
        Assert.Equal(resource.Id, await database.Context.Resources.Select(item => item.Id).SingleAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingResource()
    {
        await using var database = await CreateDatabaseAsync();
        var resource = await database.Service.CreateAsync("Meeting room", 8, CancellationToken.None);

        var deleted = await database.Service.DeleteAsync(resource.Id, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await database.Service.GetByIdAsync(resource.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenResourceDoesNotExist()
    {
        await using var database = await CreateDatabaseAsync();

        var deleted = await database.Service.DeleteAsync(42, CancellationToken.None);

        Assert.False(deleted);
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BookFlowDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new BookFlowDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(connection, context, new ResourceService(context));
    }

    private sealed class TestDatabase(
        SqliteConnection connection,
        BookFlowDbContext context,
        ResourceService service) : IAsyncDisposable
    {
        public BookFlowDbContext Context { get; } = context;

        public ResourceService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
