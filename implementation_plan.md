# Implementation Plan: .NET Aspire & Project Bootstrap

## Goal Description
Bootstrap the 6 core projects (ApiGateway, IdentityService, BookingService, catalog-service, payment-service, frontend) and set up the .NET Aspire orchestration with containerized backing services (SQL Server, MongoDB, PostgreSQL, Redis, RabbitMQ). Ensure that .NET Aspire injects the appropriate connection strings and service discovery URLs correctly. Expand the plan to detail API designs, service requirements, and connection strategies to ensure smooth interoperability.

## Proposed Changes

### 1. Solution & Aspire Orchestration
- [NEW] Create `TicketPulse.sln` in the root directory.
- [NEW] Create `src` folder.
- [NEW] Create `.NET Aspire AppHost` project in `src/AppHost`.
- [NEW] Create `.NET Aspire ServiceDefaults` project in `src/ServiceDefaults`.

### 2. .NET Microservices
- [NEW] Create `ApiGateway` as an empty ASP.NET Core Web application in `src/ApiGateway`. Add `Yarp.ReverseProxy`.
- [NEW] Create `IdentityService` as an ASP.NET Core Web API in `src/IdentityService`.
- [NEW] Create `BookingService` as an ASP.NET Core Web API in `src/BookingService`.
- All projects will reference `ServiceDefaults` for OpenTelemetry, resilience, and health checks.

### 3. Node.js / UI Services
- [NEW] Create `catalog-service` as a NestJS application in `src/catalog-service`.
- [NEW] Create `payment-service` as a Node.js/Express application in `src/payment-service`.
- [NEW] Create `frontend` as a Next.js App Router application in `src/frontend` (with Tailwind, Typescript).

### 4. Aspire AppHost Configuration
Modify `Program.cs` in `src/AppHost` to:
- Spin up containers for SQL Server, PostgreSQL, MongoDB, Redis, and RabbitMQ.
- Add the .NET projects (`ApiGateway`, `IdentityService`, `BookingService`).
- Add the Node.js projects (`catalog-service`, `payment-service`, `frontend`) using `AddNpmApp`.
- Inject connection strings (e.g. `WithReference(sql)`) and backend endpoints to the projects that need them based on the event and API contracts in `ticketpulse.md`.

### 5. Node.js Interop
- Update Node.js and Next.js projects to ensure their `package.json` has the correct `start` or `dev` commands so `AddNpmApp` can launch them.
- Optional: Add code to log or utilize the injected connection variables from Aspire (e.g., `ConnectionStrings__mongodb`) strictly for verification.

---

## Detailed Service Requirements & Connectivity

### API Gateway (YARP, .NET)
*   **Purpose**: Single entry point for the frontend, routing requests to appropriate backend services. Handles JWT extraction and validation.
*   **Required Dependencies**: `.NET Aspire ServiceDefaults`, `Yarp.ReverseProxy`, Identity/JWT libraries.
*   **Injected Configurations**:
    *   `services__identity-service__http`: URL to the Identity Service.
    *   `services__catalog-service__http`: URL to the Catalog Service.
    *   `services__booking-service__http`: URL to the Booking Service.
*   **Routing Logic**:
    *   Route `/api/auth/{**catch-all}` -> Identity Service.
    *   Route `/api/catalog/{**catch-all}` -> Catalog Service.
    *   Route `/api/booking/{**catch-all}` -> Booking Service.
    *   Ensure JWT validation is performed at the gateway, passing downstream `X-User-Id` and `X-User-Role` headers.

### Catalog Service (NestJS)
*   **Purpose**: Manage events, artists, and venues. Expose read-heavy endpoints. Cache data for performance.
*   **Required Dependencies**: MongoDB driver, Redis client (cache-aside).
*   **Injected Configurations**:
    *   `ConnectionStrings__mongodb`: MongoDB connection string.
    *   `ConnectionStrings__redis`: Redis connection string.
*   **Design APIs**:
    *   `GET /api/catalog/events`: Fetch paginated events.
    *   `GET /api/catalog/events/:id`: Fetch specific event detail (check Redis first, fallback MongoDB).
    *   `GET /api/catalog/events/search?q=`: Query MongoDB for event/artist name matches.
    *   `GET /api/catalog/venues/:id`: Fetch venue layout.

### Identity Service (.NET 10 Web API)
*   **Purpose**: Manage user accounts and issue JWT tokens for authentication.
*   **Required Dependencies**: EF Core (SQL Server), Identity/JWT libraries.
*   **Injected Configurations**:
    *   `ConnectionStrings__sql`: SQL Server connection string (using a dedicated database schema, e.g., `identitydb`).
*   **Design APIs**:
    *   `POST /api/auth/register`: Create a new user.
    *   `POST /api/auth/login`: Verify credentials and issue JWT.

### Booking Service (.NET 10 Web API)
*   **Purpose**: Handle seat holding, booking confirmation, Transactional Outbox, and Saga compensations.
*   **Required Dependencies**: EF Core (SQL Server), StackExchange.Redis (Redlock), RabbitMQ.Client / MassTransit.
*   **Injected Configurations**:
    *   `ConnectionStrings__sql`: SQL Server connection string.
    *   `ConnectionStrings__redis`: Redis connection string (for distributed locks).
    *   `ConnectionStrings__rabbitmq`: RabbitMQ connection string.
*   **Design APIs**:
    *   `POST /api/booking/hold-seat`: Acquires Redis lock for seat. Requires Auth. Body: `{ venueId, seatId, eventId }`.
    *   `POST /api/booking/book`: Confirms booking, writes to SQL Server Outbox. Requires Auth. Body: `{ seatId, eventId, paymentToken }`.
    *   `GET /api/booking/bookings/:id`: Retrieve booking. Requires Auth.
    *   `DELETE /api/booking/bookings/:id`: Cancel booking. Requires Auth.
*   **Message Broker Interop**: Needs an outbox processor to poll SQL Server and publish `BookingPending` to RabbitMQ. Needs a consumer listening for `PaymentFailed` from RabbitMQ.

### Payment & Notification Service (Node.js/Express)
*   **Purpose**: Mock payment gateway processing. Send emails/notifications. Completes Saga flow.
*   **Required Dependencies**: PostgreSQL driver (pg), amqplib (RabbitMQ).
*   **Injected Configurations**:
    *   `ConnectionStrings__postgresql`: PostgreSQL connection string.
    *   `ConnectionStrings__rabbitmq`: RabbitMQ connection string.
*   **Design APIs**: (Internal consumer, primarily acts on RabbitMQ events, but could expose webhooks).
*   **Message Broker Interop**:
    *   Consumes `BookingPending` from RabbitMQ.
    *   Executes mock payment logic (random success/fail).
    *   Publishes `PaymentSuccess` or `PaymentFailed` back to RabbitMQ.
    *   Stores logs in PostgreSQL.

### RabbitMQ Topology
*   **Exchange**: `ticketpulse.events` (Topic Exchange).
*   **Queues & Bindings**:
    *   `payment-service.booking-pending` bound to `booking.pending`.
    *   `booking-service.payment-failed` bound to `payment.failed`.
    *   `notification-service.events` bound to `payment.success`, `booking.cancelled`.

## Verification Plan

### Automated / Setup Verification
Currently, this is a blank slate. The setup commands themselves will serve as initial verification.

### Manual Verification
1. Run `dotnet run --project src/AppHost/AppHost.csproj`.
2. Open the Aspire developer dashboard (URL logged in the console).
3. Verify that **ALL 5 projects** and **ALL 5 containerized backing services** are listed as `Running` without errors.
4. Verify the logs for the Node.js and .NET apps to see that they receive their injected variables.
5. Hit `http://localhost:<api-gateway-port>/api/catalog/events` and ensure YARP successfully proxies the request to the NestJS Catalog Service.
6. Verify RabbitMQ management UI inside Aspire to ensure exchanges and queues map correctly.
