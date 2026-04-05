using AdminDashboard.Data;
using AdminDashboard.Hubs;
using AdminDashboard.Models;
using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Consumers;

public class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly DashboardDbContext _db;
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(
        DashboardDbContext db,
        IHubContext<DashboardHub> hub,
        ILogger<PaymentFailedConsumer> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var evt = context.Message;
        _logger.LogWarning("Received PaymentFailed: {BookingId}, Reason: {Reason}", evt.BookingId, evt.Reason);

        // 1. Update BookingActivity status
        var activity = await _db.BookingActivities
            .FirstOrDefaultAsync(b => b.BookingId == evt.BookingId);

        if (activity is not null)
        {
            activity.Status = "PaymentFailed";
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
        daily.FailedPayments++;

        await _db.SaveChangesAsync();

        // 3. Push to SignalR
        if (activity is not null)
        {
            await _hub.SendBookingActivity(activity);
            await _hub.SendAlert(activity);
        }
        await _hub.SendMetricsUpdate(daily);
    }
}
