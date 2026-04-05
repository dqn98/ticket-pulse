using System.ComponentModel.DataAnnotations;

namespace AdminDashboard.Models;

public class BookingActivity
{
    [Key]
    public int Id { get; set; }
    public Guid BookingId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string SeatId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, PaymentFailed, Cancelled
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTime Timestamp { get; set; }
    public DateTime LastUpdated { get; set; }
}
