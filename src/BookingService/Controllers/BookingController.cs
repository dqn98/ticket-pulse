using BookingService.Models;
using BookingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly IBookingAppService _bookingService;

    public BookingController(IBookingAppService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost("hold-seat")]
    public async Task<IActionResult> HoldSeat([FromBody] HoldSeatRequest request)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";

        var result = await _bookingService.HoldSeatAsync(request, userId);

        if (!result.Success)
        {
            return Conflict(new { Message = result.ErrorMessage });
        }

        return Ok(new { ExpiresAt = result.ExpiresAt });
    }

    [HttpPost("book")]
    public async Task<IActionResult> Book([FromBody] BookSeatRequest request)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";

        var result = await _bookingService.BookSeatAsync(request, userId);

        if (!result.Success)
        {
            if (result.ErrorMessage == "You do not hold the lock for this seat.")
            {
                return BadRequest(new { Message = result.ErrorMessage });
            }
            return Conflict(new { Message = result.ErrorMessage });
        }

        return Ok(result.Booking);
    }

    [HttpGet("bookings/{id}")]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";
        
        var booking = await _bookingService.GetBookingAsync(id, userId);
        if (booking == null)
        {
            return NotFound();
        }

        return Ok(booking);
    }

    [HttpDelete("bookings/{id}")]
    public async Task<IActionResult> CancelBooking(Guid id)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";
        
        var result = await _bookingService.CancelBookingAsync(id, userId);

        if (!result.Success)
        {
            if (result.IsNotFound)
            {
                return NotFound();
            }
            return BadRequest(new { Message = result.ErrorMessage });
        }

        return NoContent();
    }
}
