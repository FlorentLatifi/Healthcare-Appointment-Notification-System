# Deployment Guide

## Hosting Assumption

The CD pipeline assumes a **single VM with Docker Compose** as the hosting target.
This matches the project's existing `docker-compose.yml` development pattern and
avoids introducing a container orchestrator (Kubernetes, Nomad, etc.) without a
documented operational need.

If the infrastructure changes to a clustered environment (Azure Container Apps,
AWS ECS, GCP Cloud Run, or Kubernetes), the deployment step in `.github/workflows/ci.yml`
must be adapted. The image-building phase (`build-and-push`) is provider-agnostic
and should remain unchanged.

## Required Secrets (GitHub Actions)

Set these in the repository's **Settings → Secrets and variables → Actions**:

| Secret | Description |
|--------|-------------|
| `DEPLOY_SSH_HOST` | Hostname or IP of the target VM |
| `DEPLOY_SSH_USER` | SSH user (e.g., `deploy`) |
| `DEPLOY_SSH_KEY` | SSH private key (ed25519 recommended) |
| `DB_PASSWORD` | SQL Server SA password |
| `JWT_SECRET` | JWT signing key (min 32 characters) |
| `STRIPE_SECRET_KEY` | Stripe secret key (starts with `sk_live_` in production) |
| `STRIPE_PUBLISHABLE_KEY` | Stripe publishable key (starts with `pk_live_`) |

Optional:

| Secret | Description |
|--------|-------------|
| `DEPLOY_PATH` | Directory on the target VM (default: `/opt/healthcare`) |
| `OTEL_ENDPOINT` | OpenTelemetry OTLP gRPC endpoint |

## Target VM Setup (one-time)

```bash
# 1. Install Docker + Docker Compose
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"

# 2. Clone the repository
sudo mkdir -p /opt/healthcare
sudo git clone https://github.com/FlorentLatifi/Healthcare-Appointment-Notification-System.git /opt/healthcare

# 3. Create .env with secrets (used only for local troubleshooting;
#    the CD pipeline injects them via environment variables)
cp /opt/healthcare/.env.example /opt/healthcare/.env
# Edit .env with real secrets
```

## Trusted Proxies / Networks

The API uses ASP.NET Core's `ForwardedHeadersMiddleware` to extract the real
client IP from the `X-Forwarded-For` header set by nginx. Without this, every
request appears to come from the nginx container's internal IP, and rate-limiting
buckets all users together.

`docker-compose.prod.yml` sets `TrustedNetworks__0: 172.16.0.0/12` on the `api`
service, which covers the standard Docker bridge / Compose network IP range
(`172.16.0.0` – `172.31.255.255`).

If you deploy behind a different reverse proxy or on a non-Docker network,
override `TrustedNetworks` or `TrustedProxies` in the environment:

| Variable | Example | Description |
|----------|---------|-------------|
| `TrustedNetworks__0` | `172.16.0.0/12` | CIDR block whose forwarded headers are trusted |
| `TrustedProxies__0` | `10.0.1.5` | Specific proxy IP (use instead of a CIDR when the proxy has a fixed address) |

You can specify multiple entries with `__0`, `__1`, etc.

If neither is set in Production, the API logs a startup warning and rate-limiting
will not distinguish real client IPs behind the proxy.

## How the CI/CD Pipeline Works

The pipeline is defined in a **single** workflow file (`.github/workflows/ci.yml`).
This was a deliberate choice: keeping build, test, image, and deploy jobs in one
file makes the dependency chain explicit and easy to reason about.

```
unit-tests ──→ integration-tests ──┐
                                    ├──→ build-and-push ──→ deploy
frontend ───────────────────────────┘
```

1. **Trigger:** Push to `main` (or tag `v*`), or pull request against `main`.
2. **Tests run first** — on every trigger (push, PR, tag):
   - **unit-tests:** .NET format check, build, unit tests (excluding `Category=Integration`), vulnerability audit.
   - **integration-tests:** runs after unit-tests succeed; uses Testcontainers for Docker-dependent tests.
   - **frontend:** Node.js lint, test, and build.
3. **Gating:** If any test job fails, `build-and-push` is **never started** (its `needs:` block prevents it). A red CI run on `main` blocks deployment entirely.
4. **Build & Push** (`push` to `main` or `v*` tag only — skipped on PRs):
   - API Docker image → `ghcr.io/<owner>/<repo>/api:<tag>`
   - Frontend Docker image → `ghcr.io/<owner>/<repo>/frontend:<tag>`
   - Tags: `latest`, `<git-sha>`, `<semver>` (on `v*` tags)
5. **Deploy** (`push` to `main` only):
   - SSHes into the target VM using `DEPLOY_SSH_KEY`.
   - Runs `docker compose -f docker-compose.prod.yml pull` to fetch updated images.
   - Runs `docker compose -f docker-compose.prod.yml up -d --remove-orphans` to restart services.
   - Prunes unused Docker images.

## Manual Rollback

```bash
ssh deploy@$HOST
cd /opt/healthcare
# Pin a previous SHA
export API_TAG=<previous-sha>
export FRONTEND_TAG=<previous-sha>
# Override the :latest tag with a specific SHA
DB_PASSWORD='...' JWT_SECRET='...' \
  API_TAG=$API_TAG FRONTEND_TAG=$FRONTEND_TAG \
  docker compose -f docker-compose.prod.yml up -d
```
