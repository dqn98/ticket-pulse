# TicketPulse — Project Goals

A distributed, event-driven ticket booking platform built with a microservices architecture. This is a personal portfolio project designed to demonstrate production-grade distributed systems patterns in a realistic domain.

---

## Final Vision

A user opens the TicketPulse website, browses upcoming concerts and events, selects seats on an interactive venue map, holds a seat for a countdown window, and completes a mock payment — all powered by a polyglot microservices backend orchestrated by .NET Aspire. An admin monitors the entire system in real time through a dedicated dashboard.

---

## Services & Their Goals

### 1. API Gateway (`src/ApiGateway` — .NET + YARP)
- [ ] Route all frontend traffic to the correct downstream service
- [ ] Validate JWT tokens at the edge before any request reaches internal services
- [ ] Inject `X-User-Id` and `X-User-Role` headers into downstream requests
- [ ] Enforce token-bucket rate limiting to protect backend services

### 2. Identity Service (`src/IdentityService` — .NET + SQL Server)
- [ ] User registration with hashed password storage
- [ ] User login with JWT issuance (access token 15 min, refresh token 7 days)
- [ ] Role-based claims (`User`, `Admin`) embedded in JWT

### 3. Catalog Service (`src/catalog-service` — NestJS + MongoDB)
- [ ] CRUD for events, artists, and venues
- [ ] Paginated event listing with full-text search
- [ ] Venue seat layout storage (GeoJSON for interactive map)
- [ ] Redis cache-aside for read-heavy event detail queries
- [ ] Cache invalidation on event data changes

### 4. Booking Service (`src/BookingService` — .NET + SQL Server + Redis + RabbitMQ)
- [ ] `POST /hold-seat` — acquire a Redis distributed lock (Redlock) with TTL on a specific seat
- [ ] `POST /book` — confirm booking with EF Core optimistic concurrency (`RowVersion`)
- [ ] Transactional Outbox — write booking + `BookingPending` event in a single SQL transaction
- [ ] Background Outbox Processor — poll outbox table, publish to RabbitMQ, mark as processed
- [ ] Saga consumer — listen for `PaymentFailed`, run compensation (cancel booking, release lock)
- [ ] `GET /bookings/:id` and `DELETE /bookings/:id` for status lookup and manual cancellation

### 5. Payment & Notification Service (`src/payment-service` — Node.js/Express + PostgreSQL + RabbitMQ)
- [ ] Consume `BookingPending` events from RabbitMQ
- [ ] Execute mock payment logic (simulated success/failure)
- [ ] Publish `PaymentSuccess` or `PaymentFailed` back to RabbitMQ
- [ ] Log all payment transactions to PostgreSQL
- [ ] Generate and log HTML email notifications on booking confirmation or cancellation

### 6. Frontend (`src/frontend` — Next.js App Router + Tailwind + Zustand)
- [ ] Server Components for SEO-friendly event browsing and catalog pages
- [ ] Client Components for the interactive SVG seat map with real-time seat availability
- [ ] Login / registration UI calling Identity Service via the API Gateway
- [ ] Checkout flow with a Redis lock countdown timer displayed to the user
- [ ] Polling-based booking status confirmation after payment submission

### 7. Admin Dashboard (`src/AdminDashboard` — .NET Blazor + SignalR + MassTransit)
- [ ] **Main Overview** — real-time metric cards (Total Revenue, Today's Bookings, Active Users) + live ticket sales chart + critical system alert feed
- [ ] **Bookings & Payments** — live data grid tracking booking lifecycle (Pending → Confirmed / Cancelled), filters for `PaymentFailed` events
- [ ] **Event Analytics** — per-event/venue metrics showing seats held vs. confirmed, demand heatmap during ticket drops
- [ ] **System Health & Logs** — Seq API integration for exception feed, OpenTelemetry error rates, service health statuses
- [ ] CQRS read-model updated via RabbitMQ event consumers (not querying operational databases)
- [ ] SignalR hub pushing live metric updates to connected admin clients
- [ ] Secured with `Admin` role JWT authorization

---

## Infrastructure Goals (via .NET Aspire AppHost)

| Resource | Container | Used By |
|---|---|---|
| SQL Server | `mcr.microsoft.com/mssql/server` | Identity Service, Booking Service |
| MongoDB | `mongo` | Catalog Service |
| PostgreSQL | `postgres` | Payment Service |
| Redis | `redis` | Catalog Service (cache), Booking Service (Redlock) |
| RabbitMQ | `rabbitmq` | Booking Service, Payment Service, Admin Dashboard |
| Seq | `datalust/seq` | All services (centralized log aggregation) |

**Single command startup**: `dotnet run --project src/AppHost` launches all containers and services with injected connection strings and service discovery.

---

## Distributed Systems Patterns Demonstrated

| Pattern | Where |
|---|---|
| Transactional Outbox | Booking Service → RabbitMQ |
| Saga (Choreography) | Booking ↔ Payment via RabbitMQ |
| Distributed Locking (Redlock) | `POST /hold-seat` in Booking Service |
| Optimistic Concurrency | EF Core `RowVersion` on Bookings table |
| Cache-Aside | Catalog Service ↔ Redis |
| CQRS Read-Model | Admin Dashboard consuming RabbitMQ events |
| API Gateway + Header Forwarding | YARP with JWT → `X-User-Id` / `X-User-Role` |
| Token-Bucket Rate Limiting | API Gateway |
| Circuit Breaker + Retry | Polly on .NET inter-service calls |
| OpenTelemetry Distributed Tracing | All services → Aspire Dashboard / Seq |

---

## RabbitMQ Event Flow

```
BookingService                    PaymentService               BookingService (Saga)
     │                                  │                             │
     ├── BookingPending ──────────────►  │                             │
     │                                  ├── PaymentSuccess ──────►  (confirm)
     │                                  └── PaymentFailed  ──────►  (compensate)
     │                                                                │
     └── BookingCancelled ────────────────────────────────────►  NotificationService
```

All events are also consumed by the **Admin Dashboard** for real-time read-model updates.

---

## Build Phases

| Phase | Description | Status |
|---|---|---|
| 1 — System Design | Database schemas, API contracts, event payloads, JWT architecture | ✅ Done |
| 2 — Orchestration & Infra | Aspire AppHost, containers, project scaffolding, service discovery | 🔄 In Progress |
| 3 — Backend Microservices | Gateway, Identity, Catalog, Booking, Payment, Admin Dashboard | ⬜ Not Started |
| 4 — Frontend | Next.js UI with seat map, checkout flow, admin views | ⬜ Not Started |
| 5 — Observability & CI/CD | OpenTelemetry, health checks, Polly, GitHub Actions | ⬜ Not Started |
