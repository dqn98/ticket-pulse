namespace Contracts.Events;

public record PaymentSuccessEvent
{
    public Guid BookingId { get; init; }
    public string ProviderReference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
