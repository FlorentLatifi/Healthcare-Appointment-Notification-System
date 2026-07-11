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

Full strategy and vendor comparison: **[secrets-management.md](./secrets-management.md)**.

1. **Layer A — GitHub Environments** store secrets (staging vs production isolation + approval).
2. **Layer B — host `.env.secrets`** (mode `600`) is materialised at deploy time; non-secrets live in `config/production.env`.
3. **No secrets on SSH remote command lines** — runner writes a file, `scp` to `.env.secrets`, then `remote-deploy.sh` runs compose with `--env-file`.
4. **SSH keys** mode `0600`, shredded after the job; production requires **pinned** `DEPLOY_SSH_KNOWN_HOSTS`.
5. **Validate before up:** `scripts/validate-secrets-env.sh`.
6. **Immutable image tags**; Stripe test vs live never mixed; JWT/DB secrets differ per environment.
7. **Optional:** Docker secret files via `docker-compose.prod.with-file-secrets.yml` + API `docker-entrypoint.sh`.

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
