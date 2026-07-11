#!/usr/bin/env bash
# ────────────────────────────────────────────────────────────────────
# Remote deploy with health checks and automatic rollback.
# Intended to run ON the target VM (invoked over SSH by GitHub Actions).
#
# Usage:
#   ENV_FILE=.env.deploy COMPOSE_FILE=docker-compose.prod.yml \
#   API_IMAGE=ghcr.io/.../api:abc FRONTEND_IMAGE=ghcr.io/.../frontend:abc \
#   HEALTH_URL=http://127.0.0.1:8080/health \
#   ./scripts/remote-deploy.sh
# ────────────────────────────────────────────────────────────────────
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-.env.deploy}"
STATE_FILE="${STATE_FILE:-.deploy-state}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:8080/health}"
HEALTH_RETRIES="${HEALTH_RETRIES:-30}"
HEALTH_INTERVAL_SEC="${HEALTH_INTERVAL_SEC:-5}"
DEPLOY_PATH="${DEPLOY_PATH:-$(pwd)}"

API_IMAGE="${API_IMAGE:?API_IMAGE is required}"
FRONTEND_IMAGE="${FRONTEND_IMAGE:?FRONTEND_IMAGE is required}"

cd "$DEPLOY_PATH"

log() { echo "[deploy $(date -u +%H:%M:%S)] $*"; }
fail() { echo "[deploy ERROR] $*" >&2; exit 1; }

[[ -f "$COMPOSE_FILE" ]] || fail "Compose file not found: $COMPOSE_FILE"
[[ -f "$ENV_FILE" ]] || fail "Env file not found: $ENV_FILE (secrets must be provisioned securely)"

# Load previous deployment for rollback
PREV_API=""
PREV_FRONTEND=""
if [[ -f "$STATE_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$STATE_FILE"
  PREV_API="${CURRENT_API_IMAGE:-}"
  PREV_FRONTEND="${CURRENT_FRONTEND_IMAGE:-}"
  log "Previous images: api=${PREV_API:-none} frontend=${PREV_FRONTEND:-none}"
fi

export API_IMAGE FRONTEND_IMAGE
# shellcheck disable=SC1090
set -a
source "$ENV_FILE"
set +a

log "Pulling images..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" pull

log "Starting stack (api=$API_IMAGE frontend=$FRONTEND_IMAGE)..."
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --remove-orphans --wait || true

health_ok=false
log "Health checks: $HEALTH_URL (retries=$HEALTH_RETRIES interval=${HEALTH_INTERVAL_SEC}s)"
for ((i = 1; i <= HEALTH_RETRIES; i++)); do
  if curl -fsS --max-time 5 "$HEALTH_URL" >/tmp/health-body.json 2>/dev/null; then
    # Accept plain Healthy status or JSON containing Healthy
    if grep -Eqi 'Healthy|"status"[[:space:]]*:[[:space:]]*"Healthy"' /tmp/health-body.json \
      || [[ "$(cat /tmp/health-body.json)" == *"Healthy"* ]]; then
      log "Health check passed on attempt $i"
      health_ok=true
      break
    fi
    log "Attempt $i: unexpected body: $(head -c 200 /tmp/health-body.json)"
  else
    log "Attempt $i: endpoint not ready"
  fi
  sleep "$HEALTH_INTERVAL_SEC"
done

if [[ "$health_ok" != true ]]; then
  log "Health check FAILED — initiating rollback"
  if [[ -n "$PREV_API" && -n "$PREV_FRONTEND" ]]; then
    export API_IMAGE="$PREV_API"
    export FRONTEND_IMAGE="$PREV_FRONTEND"
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" pull || true
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --remove-orphans
    log "Rolled back to api=$PREV_API frontend=$PREV_FRONTEND"
  else
    log "No previous state available for rollback"
  fi
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps || true
  fail "Deployment failed health checks"
fi

# Persist successful deployment state (image refs only — never secrets)
cat > "$STATE_FILE" <<EOF
CURRENT_API_IMAGE=$API_IMAGE
CURRENT_FRONTEND_IMAGE=$FRONTEND_IMAGE
DEPLOYED_AT_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF
chmod 600 "$STATE_FILE"

docker image prune -f >/dev/null 2>&1 || true
log "Deployment successful"
