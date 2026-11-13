using BookFlow.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BookFlow.Api.Data;

public sealed class BookFlowDbContext(DbContextOptions<BookFlowDbContext> options) : DbContext(options)
{
    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<Booking> Bookings => Set<Booking>();

    private static readonly ValueConverter<DateTimeOffset, long> UtcTicksConverter = new(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(booking =>
        {
            booking.Property(item => item.CustomerName).HasMaxLength(200);
            // SQLite cannot compare DateTimeOffset values stored as TEXT, which the overlap
            // query needs. Times are always UTC, so store them as UTC ticks (INTEGER).
            booking.Property(item => item.Start).HasConversion(UtcTicksConverter);
            booking.Property(item => item.End).HasConversion(UtcTicksConverter);
            booking.Property(item => item.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
            booking.HasIndex(item => new { item.ResourceId, item.Status });
            booking.HasOne<Resource>()
                .WithMany()
                .HasForeignKey(item => item.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
