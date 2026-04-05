using AdminDashboard.Data;
using AdminDashboard.Hubs;
using AdminDashboard.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Consumers;

public class PaymentSuccessConsumer : IConsumer<PaymentSuccessEvent>
{
    private readonly DashboardDbContext _db;
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<PaymentSuccessConsumer> _logger;

    public PaymentSuccessConsumer(
        DashboardDbContext db,
        IHubContext<DashboardHub> hub,
        ILogger<PaymentSuccessConsumer> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentSuccessEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("Received PaymentSuccess: {BookingId}", evt.BookingId);

        // 1. Update BookingActivity status
        var activity = await _db.BookingActivities
            .FirstOrDefaultAsync(b => b.BookingId == evt.BookingId);

        if (activity is not null)
        {
            activity.Status = "Confirmed";
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
        daily.ConfirmedBookings++;
        daily.TotalRevenue += evt.Amount;

        // 3. Update EventMetrics
        if (activity is not null)
        {
            var eventMetrics = await _db.EventMetrics.FindAsync(activity.EventId);
            if (eventMetrics is not null)
            {
                eventMetrics.TotalConfirmed++;
                eventMetrics.TotalRevenue += evt.Amount;
                await _hub.SendEventMetricsUpdate(eventMetrics);
            }
        }

        await _db.SaveChangesAsync();

        // 4. Push to SignalR
        if (activity is not null)
            await _hub.SendBookingActivity(activity);
        await _hub.SendMetricsUpdate(daily);
    }
}
