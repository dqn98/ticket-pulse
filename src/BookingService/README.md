# BookingService

A .NET 10 Web API microservice responsible for **seat reservation and booking management** within the Ticket Pulse platform. It handles the full booking lifecycle: temporary seat holds via Redis, confirmed bookings persisted in SQL Server, and reliable event publishing through the Transactional Outbox pattern.

## Tech Stack

| Dependency | Purpose |
|---|---|
| **ASP.NET Core 10** | Web API host |
| **Entity Framework Core + SQL Server** | Persistence (`Aspire.Microsoft.EntityFrameworkCore.SqlServer`) |
| **Redis** | Distributed seat locks (`Aspire.StackExchange.Redis`) |
| **MassTransit + RabbitMQ** | Async event publishing (`MassTransit.RabbitMQ`) |
| **ServiceDefaults** | Shared Aspire defaults (OpenTelemetry, HealthChecks, Resilience) |

---

## Architecture

```
BookingController
      │
      ▼
IBookingAppService (BookingAppService)
      ├── RedisSeatLockService       ← Distributed seat hold via Redis SET NX
      └── ApplicationDbContext       ← EF Core: Bookings + OutboxMessages
                                          │
                              OutboxProcessorBackgroundService
                                          │
                                    IPublishEndpoint (MassTransit)
                                          │
                                      RabbitMQ Exchange: ticketpulse.events
```

### Key Design Patterns

- **Transactional Outbox**: Booking creation and cancellation atomically write an `OutboxMessage` to SQL Server in the same transaction. A background service polls unprocessed messages and publishes them to RabbitMQ, guaranteeing at-least-once delivery.
- **Redis Distributed Locking**: A seat hold is a Redis key `seatlock:{eventId}:{seatId}` set with `NX` (not-exists) and a 10-minute TTL. The value is the `userId`, enabling ownership validation on confirm/cancel.
- **Optimistic Concurrency**: The `Booking` entity uses a `RowVersion` column to detect concurrent double-booking attempts at the database level.

---

## Domain Entities

### `Booking`
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `UserId` | `string` | Owner of the booking |
| `SeatId` | `Guid` | FK → `Seat` |
| `EventId` | `Guid` | Event identifier |
| `Status` | `BookingStatus` | `Pending`, `Confirmed`, `Cancelled` |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp |
| `RowVersion` | `byte[]` | EF concurrency token |

### `Seat`
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `VenueId` | `Guid` | FK → `Venue` |
| `Row` | `string` | Seat row label (e.g., `"A"`) |
| `Number` | `int` | Seat number within the row |
| `Category` | `string` | Seat category (e.g., `"VIP"`, `"Standard"`) |

### `Venue`
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Venue name |
| `City` | `string` | City |
| `Address` | `string` | Street address |
| `TotalCapacity` | `int` | Total seat count |

### `OutboxMessage`
| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `EventType` | `string` | `"BookingPending"` \| `"BookingCancelled"` |
| `Payload` | `string` | JSON-serialized event data |
| `CreatedAt` | `DateTimeOffset` | Created timestamp |
| `ProcessedAt` | `DateTimeOffset?` | `null` until published to RabbitMQ |

---

## API Reference

All endpoints read the caller identity from the `X-User-Id` request header (falls back to `"anonymous"`).

**Base path:** `api/booking`

### `POST /api/booking/hold-seat`
Acquires a temporary Redis lock on a seat for 10 minutes.

**Request body:**
```json
{
  "venueId": "guid",
  "seatId":  "guid",
  "eventId": "guid"
}
```

**Responses:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `{ "expiresAt": "datetime" }` | Lock acquired |
| `409 Conflict` | `{ "message": "..." }` | Seat already held or booked |

---

### `POST /api/booking/book`
Confirms the booking for a seat the caller has locked. Creates a `Booking` (status `Pending`) and writes a `BookingPending` outbox event atomically.

**Request body:**
```json
{
  "seatId":       "guid",
  "eventId":      "guid",
  "paymentToken": "string"
}
```

**Responses:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `BookingResponse` | Booking created |
| `400 Bad Request` | `{ "message": "..." }` | Caller does not hold the lock |
| `409 Conflict` | `{ "message": "..." }` | Seat already booked / concurrency error |

---

### `GET /api/booking/bookings/{id}`
Retrieves a booking by ID. Only the booking owner can view it.

**Responses:**

| Status | Body | Condition |
|---|---|---|
| `200 OK` | `BookingResponse` | Booking found and owned by caller |
| `404 Not Found` | — | Booking not found or not owned by caller |

---

### `DELETE /api/booking/bookings/{id}`
Cancels a booking. Updates status to `Cancelled`, writes a `BookingCancelled` outbox event, and releases the Redis lock.

**Responses:**

| Status | Body | Condition |
|---|---|---|
| `204 No Content` | — | Cancelled successfully |
| `400 Bad Request` | `{ "message": "..." }` | Already cancelled |
| `404 Not Found` | — | Booking not found or not owned by caller |

---

### `BookingResponse` DTO
```json
{
  "id":        "guid",
  "eventId":   "guid",
  "seatId":    "guid",
  "status":    "Pending | Confirmed | Cancelled",
  "createdAt": "datetime"
}
```

---

## Integration Events (RabbitMQ)

Exchange: **`ticketpulse.events`** (topic)

| Event | Published when | Key fields |
|---|---|---|
| `BookingPending` | Seat successfully booked | `BookingId`, `UserId`, `SeatId`, `EventId`, `Amount`, `Currency` |
| `BookingCancelled` | Booking cancelled | `BookingId`, `SeatId`, `Reason` |

> **Consumed:** `PaymentFailed` — cancels the booking when payment processing fails (consumer stub defined, not yet implemented).

---

## Outbox Processor

`OutboxProcessorBackgroundService` runs every **2 seconds** and:
1. Queries up to `Outbox:BatchSize` (default `50`) unprocessed `OutboxMessage` rows.
2. Deserializes the JSON payload into the concrete event record.
3. Publishes via `IPublishEndpoint` to RabbitMQ.
4. Marks `ProcessedAt = UtcNow` and saves.

---

## Configuration

Connection strings for `sql` and `redis` and `rabbitmq` are injected by **.NET Aspire** at runtime. No manual connection strings are needed in `appsettings.json` for local development via Aspire.

| Key | Default | Description |
|---|---|---|
| `Outbox:BatchSize` | `50` | Max outbox messages processed per poll cycle |
| `Logging:LogLevel:Default` | `Information` | Root log level |
| `Logging:LogLevel:Microsoft.AspNetCore` | `Warning` | ASP.NET Core log level |

---

## Project Structure

```
BookingService/
├── Controllers/
│   └── BookingController.cs          # API endpoints
├── Data/
│   └── ApplicationDbContext.cs       # EF Core DbContext
├── Entities/
│   ├── Booking.cs
│   ├── BookingStatus.cs
│   ├── OutboxMessage.cs
│   ├── Seat.cs
│   └── Venue.cs
├── Events/
│   └── IntegrationEvents.cs          # MassTransit message interfaces
├── Models/
│   └── BookingDtos.cs                # Request / Response records
├── Services/
│   ├── IBookingAppService.cs
│   ├── BookingAppService.cs          # Core business logic
│   ├── RedisSeatLockService.cs       # Redis distributed locking
│   └── OutboxProcessorBackgroundService.cs
├── Program.cs
└── appsettings.json
```
