using BookingService.Data;
using BookingService.Services;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults (OpenTelemetry, HealthChecks, Resilience)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// DB Context for SQL Server
builder.AddSqlServerDbContext<ApplicationDbContext>("sql");

// Redis for locks
builder.AddRedisClient("redis");
builder.Services.AddSingleton<RedisSeatLockService>();
builder.Services.AddScoped<IBookingAppService, BookingAppService>();

// MassTransit & RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Configure consumers here (e.g., PaymentFailed)
    // x.AddConsumer<PaymentFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrEmpty(connectionString))
        {
            cfg.Host("localhost", "/", h => {
                h.Username("guest");
                h.Password("guest");
            });
        }
        else
        {
            cfg.Host(connectionString);
        }

        cfg.ConfigureEndpoints(context);
        
        // Define RabbitMQ topologies here if needed
        cfg.Message<BookingService.Events.BookingPending>(m => m.SetEntityName("ticketpulse.events"));
        cfg.Message<BookingService.Events.BookingCancelled>(m => m.SetEntityName("ticketpulse.events"));
        cfg.Publish<BookingService.Events.BookingPending>(p => p.ExchangeType = "topic");
        cfg.Publish<BookingService.Events.BookingCancelled>(p => p.ExchangeType = "topic");
    });
});

// Outbox background service
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();

var app = builder.Build();

// Migrate DB on startup (for personal project convenience)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
