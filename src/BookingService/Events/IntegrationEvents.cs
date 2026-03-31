namespace BookingService.Events;

public interface BookingPending
{
    Guid BookingId { get; }
    string UserId { get; }
    Guid SeatId { get; }
    Guid EventId { get; }
    decimal Amount { get; }
    string Currency { get; }
    DateTimeOffset Timestamp { get; }
}

public interface PaymentFailed
{
    Guid BookingId { get; }
    string Reason { get; }
    DateTimeOffset Timestamp { get; }
}

public interface BookingCancelled
{
    Guid BookingId { get; }
    Guid SeatId { get; }
    string Reason { get; }
    DateTimeOffset Timestamp { get; }
}
