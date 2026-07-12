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
   - API health: http://localhost:5171/  → `{"status":"Healthy"}`
   - Swagger UI: **only when running the API outside Docker with `ASPNETCORE_ENVIRONMENT=Development`** (Docker env is `Docker`, not Development)
   - SQL Server: `localhost:1433` (user: `sa`, password: from `.env`)
   - Redis: `localhost:6379`

4. First admin login (local Docker):

   ```sh
   docker compose logs api | findstr /I "BOOTSTRAP ADMIN"
   ```

   Username is usually `admin`. If `BOOTSTRAP_ADMIN_PASSWORD` was empty, copy the generated password from the log (one-time).

#### Database init, admin bootstrap, and demo data

On startup the API **always** applies pending EF Core migrations.

**First admin (secure bootstrap)** — no hardcoded passwords:

| Setting | Purpose |
|---------|---------|
| `Seeding__BootstrapAdmin__Enabled` | Create the first Admin only when none exists |
| `Seeding__BootstrapAdmin__Username` | Default `admin` |
| `Seeding__BootstrapAdmin__Email` | Required when enabled |
| `Seeding__BootstrapAdmin__Password` | Strong secret (min 12 chars). Set via `.env` / secret store |

- Production **requires** an explicit strong password when bootstrap is enabled (startup fails otherwise).
- Local Docker / Development may leave the password empty: a **random** password is generated once and written to the API log. Change it after first login.
- Known weak defaults (including the old `Admin123!`) are **rejected**.

**Demo doctors** only seed when:

1. `Seeding__SeedDemoData=true`, and
2. Environment is `Development`, **or** `Seeding__AllowDemoDataOutsideDevelopment=true` (used by local `docker-compose.yml` where env is `Docker`), and
3. Environment is **not** `Production` (Production always blocks demo seed).

Local compose enables demo doctors + admin bootstrap; production compose keeps both off unless you set bootstrap secrets for a one-time first-admin create.

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
| `Seeding__SeedDemoData` | No | Seed sample doctors (blocked in Production) |
| `Seeding__AllowDemoDataOutsideDevelopment` | No | Allow demo seed when env is not Development (e.g. local Docker) |
| `Seeding__BootstrapAdmin__Enabled` | No | Bootstrap first Admin if none exists |
| `Seeding__BootstrapAdmin__Username` | No | Admin username (default `admin`) |
| `Seeding__BootstrapAdmin__Email` | When bootstrap enabled | Admin email |
| `Seeding__BootstrapAdmin__Password` | Prod when bootstrap enabled | Strong password from secrets (never commit) |
| `BOOTSTRAP_ADMIN_PASSWORD` | Recommended for compose | Maps to bootstrap password in docker-compose |

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
