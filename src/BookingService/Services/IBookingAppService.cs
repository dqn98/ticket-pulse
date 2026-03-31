using BookingService.Models;

namespace BookingService.Services;

public interface IBookingAppService
{
    Task<(bool Success, string? ErrorMessage, DateTimeOffset? ExpiresAt)> HoldSeatAsync(HoldSeatRequest request, string userId);
    Task<(bool Success, string? ErrorMessage, BookingResponse? Booking)> BookSeatAsync(BookSeatRequest request, string userId);
    Task<BookingResponse?> GetBookingAsync(Guid id, string userId);
    Task<(bool Success, string? ErrorMessage, bool IsNotFound)> CancelBookingAsync(Guid id, string userId);
}
