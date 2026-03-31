using BookingService.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.City).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Venue)
                  .WithMany(v => v.Seats)
                  .HasForeignKey(e => e.VenueId);
            entity.Property(e => e.Row).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Seat)
                  .WithMany()
                  .HasForeignKey(e => e.SeatId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
            
            // Optimistic concurrency
            entity.Property(e => e.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Payload).IsRequired(); // JSON string
        });
    }
}
