using BookingService.Data;
using BookingService.Entities;
using BookingService.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookingService.Services;

public class BookingAppService : IBookingAppService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RedisSeatLockService _lockService;
    private readonly ILogger<BookingAppService> _logger;

    public BookingAppService(
        ApplicationDbContext dbContext,
        RedisSeatLockService lockService,
        ILogger<BookingAppService> logger)
    {
        _dbContext = dbContext;
        _lockService = lockService;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, DateTimeOffset? ExpiresAt)> HoldSeatAsync(HoldSeatRequest request, string userId)
    {
        var expiry = TimeSpan.FromMinutes(10);
        var acquired = await _lockService.AcquireLockAsync(request.SeatId, request.EventId, userId, expiry);

        if (!acquired)
        {
            return (false, "Seat is already held or booked by someone else.", null);
        }

        return (true, null, DateTimeOffset.UtcNow.Add(expiry));
    }

    public async Task<(bool Success, string? ErrorMessage, BookingResponse? Booking)> BookSeatAsync(BookSeatRequest request, string userId)
    {
        // 1. Verify user holds the lock
        var lockOwner = await _lockService.GetLockOwnerAsync(request.SeatId, request.EventId);
        if (lockOwner != userId)
        {
            return (false, "You do not hold the lock for this seat.", null);
        }

        // 2. Transact: Create Booking + Outbox Event
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var existingBooking = await _dbContext.Bookings
                .FirstOrDefaultAsync(b => b.SeatId == request.SeatId && b.EventId == request.EventId && b.Status != BookingStatus.Cancelled);

            if (existingBooking != null)
            {
                return (false, "Seat has already been booked.", null);
            }

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SeatId = request.SeatId,
                EventId = request.EventId,
                Status = BookingStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Bookings.Add(booking);

            var eventPayload = new 
            {
                BookingId = booking.Id,
                UserId = booking.UserId,
                SeatId = booking.SeatId,
                EventId = booking.EventId,
                Amount = 100.0m, // Mock amount
                Currency = "USD",
                Timestamp = DateTimeOffset.UtcNow
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = "BookingPending",
                Payload = JsonSerializer.Serialize(eventPayload),
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            _dbContext.OutboxMessages.Add(outboxMessage);
            
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            var response = new BookingResponse(
                booking.Id, booking.EventId, booking.SeatId, booking.Status.ToString(), booking.CreatedAt);

            return (true, null, response);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return (false, "Concurrency error. Seat already booked.", null);
        }
    }

    public async Task<BookingResponse?> GetBookingAsync(Guid id, string userId)
    {
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking == null || booking.UserId != userId)
        {
            return null;
        }

        return new BookingResponse(
            booking.Id, booking.EventId, booking.SeatId, booking.Status.ToString(), booking.CreatedAt);
    }

    public async Task<(bool Success, string? ErrorMessage, bool IsNotFound)> CancelBookingAsync(Guid id, string userId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        
        var booking = await _dbContext.Bookings.FindAsync(id);
        if (booking == null || booking.UserId != userId)
        {
            return (false, "Booking not found.", true);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return (false, "Booking already cancelled.", false);
        }

        booking.Status = BookingStatus.Cancelled;
        
        var eventPayload = new 
        {
            BookingId = booking.Id,
            SeatId = booking.SeatId,
            Reason = "User Cancelled",
            Timestamp = DateTimeOffset.UtcNow
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "BookingCancelled",
            Payload = JsonSerializer.Serialize(eventPayload),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.OutboxMessages.Add(outboxMessage);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        
        await _lockService.ReleaseLockAsync(booking.SeatId, booking.EventId, userId);

        return (true, null, false);
    }
}
