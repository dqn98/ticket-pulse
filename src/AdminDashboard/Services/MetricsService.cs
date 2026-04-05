using AdminDashboard.Data;
using AdminDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Services;

public class MetricsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MetricsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<DailyMetrics> GetTodayMetricsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await db.DailyMetrics.FindAsync(today)
            ?? new DailyMetrics { Date = today };
    }

    public async Task<List<DailyMetrics>> GetWeeklyMetricsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
        var weekAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));

        return await db.DailyMetrics
            .Where(m => m.Date >= weekAgo)
            .OrderBy(m => m.Date)
            .ToListAsync();
    }

    public async Task<List<BookingActivity>> GetRecentBookingsAsync(int count = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();

        return await db.BookingActivities
            .OrderByDescending(b => b.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<BookingActivity>> GetBookingsByStatusAsync(string status)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();

        return await db.BookingActivities
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.Timestamp)
            .Take(100)
            .ToListAsync();
    }

    public async Task<List<EventMetrics>> GetAllEventMetricsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();

        return await db.EventMetrics
            .OrderByDescending(e => e.TotalRevenue)
            .ToListAsync();
    }

    public async Task<List<BookingActivity>> GetRecentAlertsAsync(int count = 10)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();

        return await db.BookingActivities
            .Where(b => b.Status == "PaymentFailed" || b.Status == "Cancelled")
            .OrderByDescending(b => b.Timestamp)
            .Take(count)
            .ToListAsync();
    }
}
