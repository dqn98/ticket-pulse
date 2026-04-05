using AdminDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Data;

public class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options) { }

    public DbSet<DailyMetrics> DailyMetrics => Set<DailyMetrics>();
    public DbSet<BookingActivity> BookingActivities => Set<BookingActivity>();
    public DbSet<EventMetrics> EventMetrics => Set<EventMetrics>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyMetrics>(entity =>
        {
            entity.HasKey(e => e.Date);
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<BookingActivity>(entity =>
        {
            entity.HasIndex(e => e.BookingId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<EventMetrics>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");
        });
    }
}
