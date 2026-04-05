using System.ComponentModel.DataAnnotations;

namespace AdminDashboard.Models;

public class DailyMetrics
{
    [Key]
    public DateOnly Date { get; set; }
    public int TotalBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int FailedPayments { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalRevenue { get; set; }
}
