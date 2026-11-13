using BookFlow.Api.Data;
using BookFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Keep the "Async" suffix in action names so CreatedAtAction(nameof(GetByIdAsync), ...) resolves.
builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false);
builder.Services.AddOpenApi();
builder.Services.AddDbContext<BookFlowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("BookFlow") ?? "Data Source=bookflow.db"));
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BookFlowDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
