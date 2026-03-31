# TicketPulse

A personal portfolio project — a distributed, event-driven ticket booking platform built with a microservices architecture. The goal is to implement production-grade patterns (Saga, Outbox, distributed locking, CQRS-lite) in a realistic domain that exercises the full backend + frontend stack.

---

## Project overview

TicketPulse allows users to browse events, select seats on an interactive venue map, hold a seat for a short window, and complete a mock payment. The system is intentionally over-engineered for a personal project — the point is to practice distributed systems patterns, not to ship a lean MVP.

---

## Tech stack

| Layer | Technology |
|---|---|
| Orchestration | .NET Aspire (AppHost + ServiceDefaults) |
| API Gateway | .NET + YARP reverse proxy |
| Identity Service | .NET 10, EF Core, SQL Server |
| Booking Service | .NET 10, EF Core, SQL Server |
| Catalog Service | NestJS, MongoDB |
| Payment & Notification Service | Node.js / Express, PostgreSQL |
| Frontend | Next.js (App Router), Tailwind CSS, Zustand |
| Message broker | RabbitMQ |
| Cache / distributed lock | Redis (cache-aside + Redlock) |
| Auth | NextAuth.js (JWT strategy) |
| Observability | OpenTelemetry → Aspire dashboard |
| Resilience | Polly (retry + circuit breaker) |
| CI/CD | GitHub Actions |

---

## Architecture

```
Browser (Next.js)
      │
      ▼
API Gateway (YARP)
  ├── /api/auth/*     ──▶  Identity Service (.NET + SQL Server)
  ├── /api/catalog/*  ──▶  Catalog Service (NestJS + MongoDB)
  │                              └── Redis cache-aside
  └── /api/booking/*  ──▶  Booking Service (.NET + SQL Server)
                                   ├── Redis Redlock (seat hold)
                                   └── Outbox → RabbitMQ
                                                   │
                                                   ▼
                                      Payment & Notification Service
                                        (Node.js + PostgreSQL)
                                           ├── Mock payment logic
                                           ├── PaymentSuccess / PaymentFailed → RabbitMQ
                                           └── HTML email generation
                                                   │
                                        Booking Service (Saga consumer)
                                           └── Compensation on PaymentFailed
```

### Key patterns

- **Transactional Outbox** — Booking creation and the `BookingPending` event are written in a single SQL Server transaction. A background service polls the outbox table and publishes to RabbitMQ, guaranteeing at-least-once delivery without two-phase commit.
- **Saga (choreography)** — Payment Service consumes `BookingPending`, publishes `PaymentSuccess` or `PaymentFailed`. Booking Service listens for `PaymentFailed` and runs compensation (cancel booking, release seat lock).
- **Distributed locking (Redlock)** — `POST /hold-seat` acquires a Redis lock with a TTL (e.g. 10 min). Prevents two users from booking the same seat simultaneously.
- **Optimistic concurrency** — `POST /book` uses EF Core `RowVersion` to catch any race condition that slips past the Redis lock.
- **Cache-aside** — Catalog Service checks Redis before hitting MongoDB. Cache is invalidated when event data changes.

---

## Data models

### SQL Server — Identity Service

**Users** `Id | Email | PasswordHash | DisplayName | CreatedAt | Role`

### SQL Server — Booking Service

**Venues** `Id | Name | City | Address | TotalCapacity`

**Seats** `Id | VenueId (FK) | Row | Number | Category`

**Bookings** `Id | UserId (FK) | SeatId (FK) | EventId | Status | CreatedAt | RowVersion`

**Outbox** `Id | EventType | Payload (JSON) | CreatedAt | ProcessedAt`

### MongoDB — Catalog Service

**Events** `_id | Title | Description | ArtistId | VenueId | Date | TicketPriceTiers | Status`

**Artists** `_id | Name | Genre | Bio | ImageUrl`

**SeatLayouts** `_id (VenueId) | GeoJSON FeatureCollection` (seat positions for the interactive map)

### PostgreSQL — Payment Service

**PaymentLogs** `Id | BookingId | Amount | Currency | Provider | Status | ProviderReference | CreatedAt`

**NotificationHistory** `Id | BookingId | Type | Recipient | SentAt | Status`

---

## RabbitMQ event contracts

| Event | Published by | Consumed by | Key fields |
|---|---|---|---|
| `BookingPending` | Booking Service (Outbox) | Payment Service | `bookingId, userId, seatId, eventId, amount, currency, timestamp` |
| `PaymentSuccess` | Payment Service | Notification consumer | `bookingId, providerReference, timestamp` |
| `PaymentFailed` | Payment Service | Booking Service (Saga) | `bookingId, reason, timestamp` |
| `BookingCancelled` | Booking Service (Saga) | Notification consumer | `bookingId, seatId, reason, timestamp` |

---

## API endpoints

### Catalog Service (`/api/catalog`)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/events` | — | List upcoming events (paginated) |
| GET | `/events/:id` | — | Event detail |
| GET | `/events/search?q=` | — | Full-text search by event / artist name |
| GET | `/events/:id/dates` | — | Available dates with remaining seat counts |
| GET | `/venues/:id` | — | Venue detail + seat layout reference |

### Identity Service (`/api/auth`)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/register` | — | Register a new user account |
| POST | `/login` | — | Verify credentials and return JWT |

### Booking Service (`/api/booking`)

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/hold-seat` | ✓ | Acquire a Redis lock on a seat (returns `expiresAt`) |
| POST | `/book` | ✓ | Confirm booking (triggers Outbox → Saga) |
| GET | `/bookings/:id` | ✓ | Get booking status |
| DELETE | `/bookings/:id` | ✓ | Cancel a booking manually |

---

## JWT auth flow

1. Next.js frontend calls `/api/auth/login` (routed to the standalone `Identity Service`).
2. Identity Service verifies the email/password against SQL Server and mints a signed JWT with `userId`, `email`, and `role` claims. Access token expires in 15 min; refresh token in 7 days.
3. All subsequent requests go through the YARP gateway with the JWT in the `Authorization: Bearer` header. The gateway validates the JWT signature and expiry.
4. On success, the gateway injects `X-User-Id` and `X-User-Role` headers and forwards the request. Downstream services read these headers instead of re-parsing the token.
5. Invalid or expired tokens receive a `401` before reaching any internal service.

---

## Build phases

### Phase 1 — System design & API contracts
Define all database schemas, OpenAPI specs, RabbitMQ event payloads, and the JWT security architecture before writing any code.

### Phase 2 — Orchestration & infrastructure
Set up .NET Aspire AppHost with containerised SQL Server, MongoDB, PostgreSQL, Redis, and RabbitMQ. Bootstrap all five project folders and verify Aspire injects connection strings and service discovery URLs into both .NET and Node.js services.

### Phase 3 — Backend microservices
Build the API Gateway (routing, rate limiting, JWT validation), Identity Service (JWT issuance, user management), Catalog Service (CRUD, Redis caching, text search), Booking Service (EF Core, Redlock, Outbox, Saga compensation), and Payment Service (mock payment logic, RabbitMQ publishing, email generation).

### Phase 4 — Frontend
Next.js App Router with Tailwind and Zustand. Server Components for catalog browsing and SEO. Client Components for the interactive SVG seat map. Checkout flow with a Redis lock countdown timer and polling-based booking confirmation.

### Phase 5 — Observability, resilience & CI/CD
OpenTelemetry across all services (traces visible in Aspire dashboard), `/health` endpoints wired into Aspire, Polly retry + circuit-breaker on .NET inter-service calls, GitHub Actions CI for both .NET and Node.js codebases.

---

## Local development

All infrastructure and services are orchestrated by .NET Aspire. A single command starts everything:

```bash
dotnet run --project src/AppHost
```

Aspire starts containers for SQL Server, MongoDB, PostgreSQL, Redis, and RabbitMQ, then launches all five application services with injected connection strings and service discovery. The Aspire developer dashboard (typically `http://localhost:15888`) shows logs, traces, and health status for every resource.

---

## Repository structure (proposed)

```
ticketpulse/
├── src/
│   ├── AppHost/                  # .NET Aspire orchestration
│   ├── ServiceDefaults/          # Shared OTel, health, resilience
│   ├── ApiGateway/               # YARP gateway (.NET)
│   ├── IdentityService/          # .NET 10, EF Core, SQL Server
│   ├── BookingService/           # .NET 10, EF Core, SQL Server
│   ├── catalog-service/          # NestJS, MongoDB
│   ├── payment-service/          # Node.js / Express, PostgreSQL
│   └── frontend/                 # Next.js (App Router)
├── .github/
│   └── workflows/
│       ├── dotnet.yml            # Build, format, test .NET services
│       └── node.yml              # Build, lint, test Node.js services
└── docs/
    ├── architecture.md
    ├── api-contracts/            # OpenAPI spec files
    └── event-contracts.md        # RabbitMQ payload schemas
```

---

## Patterns & concepts practised

- Microservices with polyglot persistence (SQL Server, MongoDB, PostgreSQL)
- API gateway pattern with YARP
- Transactional Outbox pattern (guaranteed message delivery without 2PC)
- Saga choreography (distributed transaction with compensation)
- Distributed locking with Redlock (Redis)
- Optimistic concurrency with EF Core RowVersion
- Cache-aside pattern with Redis
- JWT authentication with header forwarding
- Token-bucket rate limiting
- OpenTelemetry distributed tracing across .NET and Node.js
- Health checks and service observability
- Polly resilience (retry + circuit breaker)
- GitHub Actions CI for mixed-language monorepo
- .NET Aspire for local dev orchestration
