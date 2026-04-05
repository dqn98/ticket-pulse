using AdminDashboard.Data;
using AdminDashboard.Hubs;
using AdminDashboard.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Consumers;

public class BookingPendingConsumer : IConsumer<BookingPendingEvent>
{
    private readonly DashboardDbContext _db;
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<BookingPendingConsumer> _logger;

    public BookingPendingConsumer(
        DashboardDbContext db,
        IHubContext<DashboardHub> hub,
        ILogger<BookingPendingConsumer> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BookingPendingEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("Received BookingPending: {BookingId}", evt.BookingId);

        // 1. Upsert BookingActivity
        var activity = new BookingActivity
        {
            BookingId = evt.BookingId,
            UserId = evt.UserId,
            EventId = evt.EventId,
            SeatId = evt.SeatId,
            Status = "Pending",
            Amount = evt.Amount,
            Currency = evt.Currency,
            Timestamp = evt.Timestamp,
            LastUpdated = DateTime.UtcNow
        };
        _db.BookingActivities.Add(activity);

        // 2. Update DailyMetrics
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daily = await _db.DailyMetrics.FindAsync(today);
        if (daily is null)
        {
            daily = new DailyMetrics { Date = today };
            _db.DailyMetrics.Add(daily);
        }
        daily.TotalBookings++;

        // 3. Update EventMetrics
        var eventMetrics = await _db.EventMetrics.FindAsync(evt.EventId);
        if (eventMetrics is null)
        {
            eventMetrics = new EventMetrics { EventId = evt.EventId };
            _db.EventMetrics.Add(eventMetrics);
        }
        eventMetrics.TotalHeld++;

        await _db.SaveChangesAsync();

        // 4. Push to SignalR
        await _hub.SendBookingActivity(activity);
        await _hub.SendMetricsUpdate(daily);
        await _hub.SendEventMetricsUpdate(eventMetrics);
    }
}
