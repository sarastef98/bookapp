using BookFlow.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookFlow.Api.Data;

public sealed class BookFlowDbContext(DbContextOptions<BookFlowDbContext> options) : DbContext(options)
{
    public DbSet<Resource> Resources => Set<Resource>();
}
