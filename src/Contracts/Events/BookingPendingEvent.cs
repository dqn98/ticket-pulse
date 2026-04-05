namespace Contracts.Events;

public record BookingPendingEvent
{
    public Guid BookingId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string SeatId { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
