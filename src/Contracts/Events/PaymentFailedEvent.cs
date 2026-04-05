namespace Contracts.Events;

public record PaymentFailedEvent
{
    public Guid BookingId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
