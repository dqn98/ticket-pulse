using BookingService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using BookingService.Events;

namespace BookingService.Services;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<OutboxProcessorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var batchSize = _configuration.GetValue<int>("Outbox:BatchSize", 50);

                var messages = await dbContext.OutboxMessages
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(message, publishEndpoint, stoppingToken);
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }

                if (messages.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages.");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(Entities.OutboxMessage message, IPublishEndpoint publishEndpoint, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case "BookingPending":
                var pendingEvent = JsonSerializer.Deserialize<BookingPendingEvent>(message.Payload);
                if (pendingEvent != null)
                {
                    await publishEndpoint.Publish<BookingPending>(pendingEvent, cancellationToken);
                }
                break;
            case "BookingCancelled":
                var cancelledEvent = JsonSerializer.Deserialize<BookingCancelledEvent>(message.Payload);
                if (cancelledEvent != null)
                {
                    await publishEndpoint.Publish<BookingCancelled>(cancelledEvent, cancellationToken);
                }
                break;
            default:
                _logger.LogWarning("Unknown event type: {EventType}", message.EventType);
                break;
        }
    }

    // Concrete records for deserialization
    private record BookingPendingEvent(Guid BookingId, string UserId, Guid SeatId, Guid EventId, decimal Amount, string Currency, DateTimeOffset Timestamp) : BookingPending;
    private record BookingCancelledEvent(Guid BookingId, Guid SeatId, string Reason, DateTimeOffset Timestamp) : BookingCancelled;
}
