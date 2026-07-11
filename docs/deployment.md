# Deployment Guide

## Pipeline overview

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `CI` | PR + push to `main` | Architecture, unit, integration, frontend tests |
| `Build and Publish` | Green CI on `main`, tags `v*`, manual | Build images, **Trivy** scan, **SBOM**, push GHCR |
| `Deploy Staging` | After main build (auto) or manual | Deploy immutable tags to staging |
| `Deploy Production` | After `v*` tag build, or manual confirm | Deploy with environment approval + rollback |

```
PR / main ──► CI ──► (main only) Build+Trivy+SBOM+Push ──► Staging deploy
tag v*    ──► Build+Trivy+SBOM+Push ──► Production deploy (approval)
```

## GitHub Environments (required)

Create two environments under **Settings → Environments**:

### `staging`

- **Deployment branches:** `main` (optional restriction)
- **Secrets** (environment-scoped):

| Secret | Required | Description |
|--------|----------|-------------|
| `DEPLOY_SSH_HOST` | yes | Staging host |
| `DEPLOY_SSH_USER` | yes | SSH user |
| `DEPLOY_SSH_KEY` | yes | Private key (ed25519) |
| `DEPLOY_SSH_KNOWN_HOSTS` | recommended | Output of `ssh-keyscan -H host` |
| `DEPLOY_PATH` | no | Default `/opt/healthcare-staging` |
| `DB_PASSWORD` | yes | SQL SA password (staging DB only) |
| `JWT_SECRET` | yes | ≥ 32 chars; **different from production** |
| `STRIPE_SECRET_KEY` | no | Prefer `sk_test_…` |
| `STRIPE_PUBLISHABLE_KEY` | no | Prefer `pk_test_…` |
| `GHCR_PULL_TOKEN` | no | PAT with `read:packages` if host cannot use GITHUB_TOKEN |
| `GHCR_PULL_USER` | no | User for GHCR pull |
| `BOOTSTRAP_ADMIN_*` | no | One-time admin bootstrap |

**Variables:**

| Variable | Description |
|----------|-------------|
| `STAGING_PUBLIC_URL` | Shown on GitHub Environments UI |
| `STAGING_FRONTEND_URL` | CORS / AllowedOrigins |
| `STAGING_HEALTH_URL` | Default `http://127.0.0.1:8081/health` |

### `production`

- **Required reviewers:** enable (1+ reviewers)
- **Wait timer:** optional
- **Deployment branches:** tags `v*` only (recommended)
- **Secrets:** same names as staging but **different values**
- **`DEPLOY_SSH_KNOWN_HOSTS`:** **required** (workflow fails without pin)
- **`DEPLOY_PATH`:** default `/opt/healthcare`

**Variables:**

| Variable | Description |
|----------|-------------|
| `PRODUCTION_PUBLIC_URL` | Public site URL |
| `PRODUCTION_FRONTEND_URL` | CORS origin (**required**) |
| `PRODUCTION_HEALTH_URL` | Default `http://127.0.0.1:8080/health` |
| `PRODUCTION_PUBLIC_HEALTH_URL` | Optional external smoke URL |

> **Never** put production secrets in repository-level Actions secrets if staging can read them. Prefer environment secrets only.

## Secret handling practices

1. **No secrets on SSH remote command lines for app config** — values are written to a local env file on the runner, `scp`’d to `.env.deploy` with mode `600`, then sourced by `scripts/remote-deploy.sh`.
2. **SSH private keys** written to disk with `0600` and shredded after the job.
3. **Known hosts pinned** in production (`DEPLOY_SSH_KNOWN_HOSTS`); staging warns if missing.
4. **GHCR pull** uses `docker login --password-stdin` on the host; prefer a read-only PAT (`GHCR_PULL_TOKEN`) for long-lived hosts.
5. **Immutable image tags** (12-char git SHA) — never deploy `:latest` for production rollbacks.
6. **Stripe:** staging = test keys; production = live keys; never mix.
7. **JWT / DB passwords** must differ between staging and production.

## Deploy strategy (health + rollback)

`scripts/remote-deploy.sh`:

1. Reads previous images from `.deploy-state` (if any)
2. `docker compose pull` for `API_IMAGE` / `FRONTEND_IMAGE`
3. `docker compose up -d`
4. Polls `HEALTH_URL` until Healthy (configurable retries)
5. **On failure:** redeploys previous images from `.deploy-state` and exits non-zero
6. **On success:** updates `.deploy-state` (image refs only — no secrets)

Compose files:

| File | Ports | Notes |
|------|-------|-------|
| `docker-compose.staging.yml` | API `8081`, FE `8088` | Separate DB volume, Redis prefix `Staging:` |
| `docker-compose.prod.yml` | API `8080`, FE `80` | No public SQL/Redis ports |

## Trivy + SBOM

On every image build:

- **Trivy** scans OS + library vulns, secrets, misconfig  
  - Fails the pipeline on **CRITICAL/HIGH** (unfixed ignored)
  - SARIF uploaded to GitHub Code Scanning (when enabled)
- **SPDX JSON SBOM** generated for API and frontend → Actions artifacts (90 days)
- Buildx **provenance** (`mode=max`) + **SBOM attestations** pushed with images

## Manual operations

```bash
# Staging redeploy of a known good SHA
gh workflow run "Deploy Staging" -f image_tag=abc123def456

# Production manual (still requires environment approval)
gh workflow run "Deploy Production" \
  -f image_tag=abc123def456 \
  -f confirm=deploy-production
```

## Target VM setup (one-time)

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker "$USER"

# Staging
sudo mkdir -p /opt/healthcare-staging/scripts
sudo chown -R deploy:deploy /opt/healthcare-staging

# Production
sudo mkdir -p /opt/healthcare/scripts
sudo chown -R deploy:deploy /opt/healthcare

# Pin host key for GitHub secret DEPLOY_SSH_KNOWN_HOSTS
ssh-keyscan -H your.staging.host
ssh-keyscan -H your.production.host
```

## First-admin bootstrap (production)

1. Set environment secrets: `BOOTSTRAP_ADMIN_ENABLED=true`, email, strong password  
2. Deploy once  
3. Set `BOOTSTRAP_ADMIN_ENABLED=false` for subsequent deploys  
4. Never enable demo seeding in production  

## Public endpoints

| Path | Auth | Purpose |
|------|------|---------|
| `/health` | Anonymous | Liveness |
| `/health/details` | Admin JWT | Dependency breakdown |
| `8080` / `80` (prod) | App rules | Traffic |
| `8081` / `8088` (staging) | App rules | Staging traffic |
