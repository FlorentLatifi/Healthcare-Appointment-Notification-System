# Healthcare Appointment Notification System

![Build Status](https://github.com/FlorentLatifi/Healthcare-Appointment-Notification-System/actions/workflows/build-and-test.yml/badge.svg)

## Overview

A comprehensive healthcare appointment notification system built with ASP.NET Core 8.0, following clean architecture principles with Domain, Application, Adapters, and Presentation layers. The system supports appointment booking, cancellation, confirmation, payment processing (Stripe), email notifications, Redis caching, rate limiting, and a React 19 frontend.

## Technology Stack

- **Backend**: ASP.NET Core 8.0
- **Testing Framework**: xUnit with Moq and FluentAssertions
- **Database**: SQL Server with Entity Framework Core 8.0
- **Email**: MailKit
- **Authentication**: JWT Bearer
- **Caching**: Redis with StackExchange.Redis
- **API Documentation**: Swagger/Swashbuckle
- **Logging**: Serilog
- **Validation**: FluentValidation
- **Payment Processing**: Stripe.NET
- **Frontend**: React 19, Vite 7, Tailwind CSS 4, React Router 7

## Project Structure

```
Healthcare.AppointmentSystem/
├── Healthcare.Domain/               # Core business entities, value objects, enums, domain events, port interfaces
├── Healthcare.Application/          # CQRS commands/queries, handlers, DTOs, application services, pricing
├── Healthcare.Adapters/             # EF Core persistence, JWT auth, Redis caching/distributed locking,
│                                     # Stripe payments, MailKit email, domain event handlers
├── Healthcare.Presentation.API/     # ASP.NET Core Web API — controllers, middleware, Swagger, background
│                                     # services (reminders, outbox relay, DB seeding)
├── Healthcare.UnitTests/            # Unit tests (xUnit + Moq) for all layers + architecture enforcement
└── Healthcare.IntegrationTests/     # End-to-end tests using Testcontainers (real SQL Server + Redis)
healthcare-frontend/                 # React 19 + Vite 7 SPA — appointment booking, doctor dashboard, auth
```

## Local Development

### Docker Compose (recommended)

A `docker-compose.yml` at the repo root spins up the full stack (API + SQL Server + Redis + frontend) with one command.

#### Prerequisites

- Docker & Docker Compose

#### Setup

1. Copy `.env.example` to `.env` and fill in the secrets:

   ```sh
   cp .env.example .env
   ```

2. Start the stack:

   ```sh
   docker compose up --build
   ```

3. Access the services:

   - Frontend: http://localhost:5173
   - API: http://localhost:5171
   - Swagger UI: http://localhost:5171/index.html
   - SQL Server: `localhost:1433` (user: `sa`, password: from `.env`)
   - Redis: `localhost:6379`

#### Seeded demo data

On first startup (when the `Doctors` table is empty), the API automatically:

- Runs EF Core migrations
- Creates an **Admin** user:
  - Username: `admin`
  - Password: `Admin123!`
  - Email: `admin@healthcareclinic.com`
- Creates 4 sample doctors across different specialties (General Practice, Cardiology, Pediatrics, Neurology) with realistic consultation fees

Seeding is controlled by the `SeedDemoData` config flag (set to `true` only in the Docker Compose environment). It defaults to `false` in all other environments.

### Manual (without Docker)

#### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 22+](https://nodejs.org/)
- SQL Server (local instance or remote)
- Redis (local instance or remote)

#### Backend

```sh
cd Healthcare.AppointmentSystem

# Update the connection string in appsettings.Development.json for your local SQL Server & Redis
dotnet restore
dotnet run --project Healthcare.Presentation.API
```

The API starts at `http://localhost:5171` with Swagger at `http://localhost:5171/index.html`.

#### Frontend

```sh
cd healthcare-frontend
npm install
npm run dev
```

The frontend starts at `http://localhost:5173` and expects the API at `http://localhost:5171/api/v1` (configurable via `VITE_API_BASE_URL` in `healthcare-frontend/.env`).

## Environment Variables / Configuration Keys

| Key | Required | Description |
|-----|----------|-------------|
| `Jwt__Secret` | **Yes** | JWT signing key (minimum 32 characters) |
| `Stripe__SecretKey` | **Yes** | Stripe secret key (starts with `sk_test_` in test mode) |
| `Stripe__PublishableKey` | **Yes** | Stripe publishable key (starts with `pk_test_` in test mode) |
| `Redis__ConnectionString` | **Yes** | Redis connection string (e.g., `localhost:6379,abortConnect=false`) |
| `AllowedOrigins` | **Yes** | Comma-separated list of allowed CORS origins |
| `ConnectionStrings__DefaultConnection` | **Yes** | SQL Server connection string |
| `TrustedProxies` | **Yes** | Comma-separated list of reverse-proxy IPs; prevents rate-limit collapse behind a proxy |
| `TrustedNetworks` | **Yes** | Comma-separated CIDR networks (e.g., `10.0.0.0/8`); alternative to `TrustedProxies` |
| `UseOutboxForDomainEvents` | No | Defaults to `true` outside Development; set `false` to disable reliable event delivery |
| `Otel__Endpoint` | No | OTLP gRPC endpoint (e.g., `http://localhost:4317`); when unset, traces/metrics fall back to console |
| `SeedDemoData` | No | Set to `true` to seed demo data on startup (Docker Compose only) |

Replace the Stripe test-mode placeholders with real keys from your [Stripe dashboard](https://dashboard.stripe.com/test/apikeys). The app starts without valid Stripe keys, but payment features will fail.

## Running Tests

### Backend

```sh
cd Healthcare.AppointmentSystem

# All tests
dotnet test

# Unit tests only (faster, no Docker required)
dotnet test --filter "Category!=Integration"

# Integration tests only (requires Docker — Testcontainers will pull SQL Server & Redis images)
dotnet test --filter "Category=Integration"
```

### Frontend

```sh
cd healthcare-frontend
npm run test        # single run (CI)
npm run test:watch  # watch mode
```
