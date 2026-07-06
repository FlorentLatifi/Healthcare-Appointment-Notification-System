# Healthcare Appointment Notification System

![Build Status](https://github.com/FlorentLatifi/Healthcare-Appointment-Notification-System/actions/workflows/build-and-test.yml/badge.svg)

## Overview

A comprehensive healthcare appointment notification system built with ASP.NET Core 8.0, following clean architecture principles with Domain, Application, Adapters, and Presentation layers.

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

## Local Development with Docker

A `docker-compose.yml` at the repo root spins up the full stack (API + SQL Server + Redis + frontend) with one command.

### Prerequisites

- Docker & Docker Compose

### Setup

1. Copy `.env.example` to `.env` and fill in the secrets:
   ```
   cp .env.example .env
   ```

2. Start the stack:
   ```
   docker compose up --build
   ```

3. Access the services:
   - Frontend: http://localhost:5173
   - API: http://localhost:5171
   - Swagger UI: http://localhost:5171/index.html
   - SQL Server: `localhost:1433` (user: `sa`, password: from `.env`)
   - Redis: `localhost:6379`

### Seeded demo data

On first startup (when the `Doctors` table is empty), the API automatically:

- Runs EF Core migrations
- Creates an **Admin** user:
  - Username: `admin`
  - Password: `Admin123!`
  - Email: `admin@healthcareclinic.com`
- Creates 4 sample doctors across different specialties (General Practice, Cardiology, Pediatrics, Neurology) with realistic consultation fees

Seeding is controlled by the `SeedDemoData` config flag (set to `true` only in the Docker Compose environment). It defaults to `false` in all other environments.

### Environment variables

| Variable | Description |
|----------|-------------|
| `DB_PASSWORD` | SQL Server SA password (min 8 chars, complex) |
| `JWT_SECRET` | JWT signing key (min 32 characters) |
| `STRIPE_SECRET_KEY` | Stripe secret key (starts with `sk_test_` in test mode) |
| `STRIPE_PUBLISHABLE_KEY` | Stripe publishable key (starts with `pk_test_` in test mode) |

Replace the Stripe test-mode placeholders with real keys from your [Stripe dashboard](https://dashboard.stripe.com/test/apikeys). The app will still start without valid Stripe keys, but payment features will fail.

## Project Structure
