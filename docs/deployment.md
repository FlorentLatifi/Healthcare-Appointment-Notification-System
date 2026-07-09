# Deployment Guide

## Hosting Assumption

The CD pipeline assumes a **single VM with Docker Compose** as the hosting target.
This matches the project's existing `docker-compose.yml` development pattern and
avoids introducing a container orchestrator (Kubernetes, Nomad, etc.) without a
documented operational need.

If the infrastructure changes to a clustered environment (Azure Container Apps,
AWS ECS, GCP Cloud Run, or Kubernetes), the deployment step in `.github/workflows/cd.yml`
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

## How the CD Pipeline Works

1. **Trigger:** Push to `main` (or tag `v*`).
2. **Build & Push:**
   - API Docker image → `ghcr.io/<owner>/<repo>/api:<tag>`
   - Frontend Docker image → `ghcr.io/<owner>/<repo>/frontend:<tag>`
   - Tags: `latest`, `<git-sha>`, `<semver>` (on `v*` tags)
3. **Deploy:**
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
