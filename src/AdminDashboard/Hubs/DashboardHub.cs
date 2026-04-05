using AdminDashboard.Models;
using Microsoft.AspNetCore.SignalR;

namespace AdminDashboard.Hubs;

public class DashboardHub : Hub
{
    public async Task RequestMetrics()
    {
        // Clients can call this on connect to request initial data push
        await Clients.Caller.SendAsync("MetricsRequested");
    }
}

// Static helper to broadcast from consumers without injecting the Hub directly
public static class DashboardHubExtensions
{
    public static async Task SendMetricsUpdate(this IHubContext<DashboardHub> hub, DailyMetrics metrics)
    {
        await hub.Clients.All.SendAsync("ReceiveMetricsUpdate", metrics);
    }

    public static async Task SendBookingActivity(this IHubContext<DashboardHub> hub, BookingActivity activity)
    {
        await hub.Clients.All.SendAsync("ReceiveBookingActivity", activity);
    }

    public static async Task SendEventMetricsUpdate(this IHubContext<DashboardHub> hub, EventMetrics metrics)
    {
        await hub.Clients.All.SendAsync("ReceiveEventMetricsUpdate", metrics);
    }

    public static async Task SendAlert(this IHubContext<DashboardHub> hub, BookingActivity alert)
    {
        await hub.Clients.All.SendAsync("ReceiveAlert", alert);
    }
}
