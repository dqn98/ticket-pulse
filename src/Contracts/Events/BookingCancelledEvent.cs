namespace Contracts.Events;

public record BookingCancelledEvent
{
    public Guid BookingId { get; init; }
    public string SeatId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
