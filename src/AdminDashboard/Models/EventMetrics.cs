using System.ComponentModel.DataAnnotations;

namespace AdminDashboard.Models;

public class EventMetrics
{
    [Key]
    public string EventId { get; set; } = string.Empty;
    public int TotalHeld { get; set; }
    public int TotalConfirmed { get; set; }
    public int TotalCancelled { get; set; }
    public decimal TotalRevenue { get; set; }
}
