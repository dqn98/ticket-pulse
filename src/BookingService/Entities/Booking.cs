namespace BookingService.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid SeatId { get; set; }
    public Guid EventId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    public byte[] RowVersion { get; set; } = null!;

    public Seat Seat { get; set; } = null!;
}
