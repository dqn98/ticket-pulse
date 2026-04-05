using AdminDashboard.Components;
using AdminDashboard.Consumers;
using AdminDashboard.Data;
using AdminDashboard.Hubs;
using AdminDashboard.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SQLite Read-Model Database
builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlite("Data Source=dashboard.db"));

// MassTransit + RabbitMQ Consumers
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingPendingConsumer>();
    x.AddConsumer<PaymentSuccessConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<BookingCancelledConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("rabbitmq");
        if (!string.IsNullOrEmpty(connectionString))
        {
            cfg.Host(new Uri(connectionString));
        }

        cfg.ReceiveEndpoint("admin-dashboard-booking-pending", e =>
        {
            e.ConfigureConsumer<BookingPendingConsumer>(context);
        });

        cfg.ReceiveEndpoint("admin-dashboard-payment-success", e =>
        {
            e.ConfigureConsumer<PaymentSuccessConsumer>(context);
        });

        cfg.ReceiveEndpoint("admin-dashboard-payment-failed", e =>
        {
            e.ConfigureConsumer<PaymentFailedConsumer>(context);
        });

        cfg.ReceiveEndpoint("admin-dashboard-booking-cancelled", e =>
        {
            e.ConfigureConsumer<BookingCancelledConsumer>(context);
        });
    });
});

// Application Services
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddHttpClient<SeqLogService>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Ensure SQLite database is created on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

// Map SignalR hub
app.MapHub<DashboardHub>("/hubs/dashboard");

app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
