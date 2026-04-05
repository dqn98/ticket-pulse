using AdminDashboard.Data;
using AdminDashboard.Hubs;
using AdminDashboard.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Consumers;

public class BookingCancelledConsumer : IConsumer<BookingCancelledEvent>
{
    private readonly DashboardDbContext _db;
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<BookingCancelledConsumer> _logger;

    public BookingCancelledConsumer(
        DashboardDbContext db,
        IHubContext<DashboardHub> hub,
        ILogger<BookingCancelledConsumer> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BookingCancelledEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("Received BookingCancelled: {BookingId}", evt.BookingId);

        // 1. Update BookingActivity status
        var activity = await _db.BookingActivities
            .FirstOrDefaultAsync(b => b.BookingId == evt.BookingId);

        if (activity is not null)
        {
            activity.Status = "Cancelled";
            activity.LastUpdated = DateTime.UtcNow;
        }

        // 2. Update DailyMetrics
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daily = await _db.DailyMetrics.FindAsync(today);
        if (daily is null)
        {
            daily = new DailyMetrics { Date = today };
            _db.DailyMetrics.Add(daily);
        }
        daily.CancelledBookings++;

        // 3. Update EventMetrics
        var eventId = activity?.EventId;
        if (eventId is not null)
        {
            var eventMetrics = await _db.EventMetrics.FindAsync(eventId);
            if (eventMetrics is not null)
            {
                eventMetrics.TotalCancelled++;
                if (eventMetrics.TotalHeld > 0) eventMetrics.TotalHeld--;
                await _hub.SendEventMetricsUpdate(eventMetrics);
            }
        }

        await _db.SaveChangesAsync();

        // 4. Push to SignalR
        if (activity is not null)
        {
            await _hub.SendBookingActivity(activity);
            await _hub.SendAlert(activity);
        }
        await _hub.SendMetricsUpdate(daily);
    }
}
