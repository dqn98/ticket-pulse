namespace BookingService.Models;

public record HoldSeatRequest(Guid VenueId, Guid SeatId, Guid EventId);
public record BookSeatRequest(Guid SeatId, Guid EventId, string PaymentToken);

public record BookingResponse(
    Guid Id,
    Guid EventId,
    Guid SeatId,
    string Status,
    DateTimeOffset CreatedAt
);
