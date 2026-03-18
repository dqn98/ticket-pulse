# Implementation Plan: .NET Aspire & Project Bootstrap

## Goal Description
Bootstrap the 5 core projects (ApiGateway, BookingService, catalog-service, payment-service, frontend) and set up the .NET Aspire orchestration with containerized backing services (SQL Server, MongoDB, PostgreSQL, Redis, RabbitMQ). Ensure that .NET Aspire injects the appropriate connection strings and service discovery URLs correctly.

## Proposed Changes

### 1. Solution & Aspire Orchestration
- [NEW] Create `TicketPulse.sln` in the root directory.
- [NEW] Create `src` folder.
- [NEW] Create `.NET Aspire AppHost` project in `src/AppHost`.
- [NEW] Create `.NET Aspire ServiceDefaults` project in `src/ServiceDefaults`.

### 2. .NET Microservices
- [NEW] Create `ApiGateway` as an empty ASP.NET Core Web application in `src/ApiGateway`. Add `Yarp.ReverseProxy`.
- [NEW] Create `BookingService` as an ASP.NET Core Web API in `src/BookingService`.
- Both projects will reference `ServiceDefaults` for OpenTelemetry, resilience, and health checks.

### 3. Node.js / UI Services
- [NEW] Create `catalog-service` as a NestJS application in `src/catalog-service`.
- [NEW] Create `payment-service` as a Node.js/Express application in `src/payment-service`.
- [NEW] Create `frontend` as a Next.js App Router application in `src/frontend` (with Tailwind, Typescript).

### 4. Aspire AppHost Configuration
Modify `Program.cs` in `src/AppHost` to:
- Spin up containers for SQL Server, PostgreSQL, MongoDB, Redis, and RabbitMQ.
- Add the .NET projects (`ApiGateway`, `BookingService`).
- Add the Node.js projects (`catalog-service`, `payment-service`, `frontend`) using `AddNpmApp`.
- Inject connection strings (e.g. `WithReference(sql)`) and backend endpoints to the projects that need them based on the event and API contracts in [ticketpulse.md](file:///d:/personalproject/ticket-pulse/ticketpulse.md).

### 5. Node.js Interop
- Update Node.js and Next.js projects to ensure their `package.json` has the correct `start` or `dev` commands so `AddNpmApp` can launch them.
- Optional: Add code to log or utilize the injected connection variables from Aspire (e.g., `ConnectionStrings__mongodb`) strictly for verification.

## Verification Plan

### Automated / Setup Verification
Currently, this is a blank slate. The setup commands themselves will serve as initial verification.

### Manual Verification
1. Run `dotnet run --project src/AppHost/AppHost.csproj`.
2. Open the Aspire developer dashboard (URL logged in the console).
3. Verify that **ALL 5 projects** and **ALL 5 containerized backing services** are listed as `Running` without errors.
4. Verify the logs for the Node.js and .NET apps to see that they receive their injected variables.
