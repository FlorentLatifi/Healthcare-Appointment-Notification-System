#!/usr/bin/env bash
# ────────────────────────────────────────────────────────────────────
# Remote deploy with health checks and automatic rollback.
# Secrets: SECRETS_ENV_FILE (default .env.secrets), mode 600.
# Config:  CONFIG_ENV_FILE (default config/production.env).
# ────────────────────────────────────────────────────────────────────
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
SECRETS_ENV_FILE="${SECRETS_ENV_FILE:-.env.secrets}"
# Back-compat with older workflows that used ENV_FILE
if [[ -n "${ENV_FILE:-}" && "${ENV_FILE}" != "${SECRETS_ENV_FILE}" ]]; then
  SECRETS_ENV_FILE="$ENV_FILE"
fi
CONFIG_ENV_FILE="${CONFIG_ENV_FILE:-config/production.env}"
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
[[ -f "$SECRETS_ENV_FILE" ]] || fail "Secrets file not found: $SECRETS_ENV_FILE"
[[ "$(stat -c %a "$SECRETS_ENV_FILE" 2>/dev/null || stat -f %A "$SECRETS_ENV_FILE")" =~ ^(600|400)$ ]] \
  || log "WARN: $SECRETS_ENV_FILE should be mode 600 (current may be looser)"

if [[ -x ./scripts/validate-secrets-env.sh ]]; then
  ENV_LABEL=production
  if [[ "$COMPOSE_FILE" == *"staging"* ]]; then ENV_LABEL=staging; fi
  ./scripts/validate-secrets-env.sh "$SECRETS_ENV_FILE" "$ENV_LABEL" || fail "secrets validation failed"
fi

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
export SECRETS_ENV_FILE CONFIG_ENV_FILE

COMPOSE_ENV_ARGS=(--env-file "$SECRETS_ENV_FILE")
if [[ -f "$CONFIG_ENV_FILE" ]]; then
  COMPOSE_ENV_ARGS+=(--env-file "$CONFIG_ENV_FILE")
fi

compose() {
  docker compose "${COMPOSE_ENV_ARGS[@]}" -f "$COMPOSE_FILE" "$@"
}

log "Pulling images..."
compose pull

log "Starting stack (api=$API_IMAGE frontend=$FRONTEND_IMAGE)..."
compose up -d --remove-orphans --wait || true

health_ok=false
log "Health checks: $HEALTH_URL (retries=$HEALTH_RETRIES interval=${HEALTH_INTERVAL_SEC}s)"
for ((i = 1; i <= HEALTH_RETRIES; i++)); do
  if curl -fsS --max-time 5 "$HEALTH_URL" >/tmp/health-body.json 2>/dev/null; then
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
    compose pull || true
    compose up -d --remove-orphans
    log "Rolled back to api=$PREV_API frontend=$PREV_FRONTEND"
  else
    log "No previous state available for rollback"
  fi
  compose ps || true
  fail "Deployment failed health checks"
fi

cat > "$STATE_FILE" <<EOF
CURRENT_API_IMAGE=$API_IMAGE
CURRENT_FRONTEND_IMAGE=$FRONTEND_IMAGE
DEPLOYED_AT_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF
chmod 600 "$STATE_FILE"

docker image prune -f >/dev/null 2>&1 || true
log "Deployment successful"
