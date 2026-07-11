#!/usr/bin/env bash
# Validate a secrets env file before deploy (does not print secret values).
# Usage: ./scripts/validate-secrets-env.sh .env.secrets [production|staging]
set -euo pipefail

FILE="${1:?env file required}"
ENV_NAME="${2:-production}"

[[ -f "$FILE" ]] || { echo "ERROR: $FILE not found"; exit 1; }

# shellcheck disable=SC1090
set -a
# shellcheck source=/dev/null
source "$FILE"
set +a

errors=0
warn() { echo "WARN: $*"; }
fail() { echo "ERROR: $*"; errors=$((errors + 1)); }

require() {
  local name="$1"
  local val="${!name:-}"
  if [[ -z "$val" ]]; then
    fail "$name is required"
  fi
}

require DB_PASSWORD
require JWT_SECRET

if [[ ${#JWT_SECRET} -lt 32 ]]; then
  fail "JWT_SECRET length ${#JWT_SECRET} < 32"
fi

if [[ "$JWT_SECRET" == *"change-me"* ]] || [[ "$JWT_SECRET" == *"example"* ]]; then
  fail "JWT_SECRET looks like a placeholder"
fi

if [[ "$DB_PASSWORD" == *"change-me"* ]] || [[ ${#DB_PASSWORD} -lt 12 ]]; then
  fail "DB_PASSWORD is weak or placeholder (min 12 chars)"
fi

if [[ "$ENV_NAME" == "production" ]]; then
  if [[ "${STRIPE_SECRET_KEY:-}" == sk_test_* ]]; then
    warn "STRIPE_SECRET_KEY is a test key in production"
  fi
  if [[ "${STRIPE_SECRET_KEY:-}" == sk_live_* ]]; then
    echo "OK: live Stripe secret key prefix"
  fi
  if [[ "${BOOTSTRAP_ADMIN_ENABLED:-false}" == "true" ]]; then
    require BOOTSTRAP_ADMIN_PASSWORD
    require BOOTSTRAP_ADMIN_EMAIL
    if [[ ${#BOOTSTRAP_ADMIN_PASSWORD} -lt 12 ]]; then
      fail "BOOTSTRAP_ADMIN_PASSWORD min 12 chars"
    fi
    warn "BOOTSTRAP_ADMIN_ENABLED=true — disable after first successful deploy"
  fi
else
  if [[ "${STRIPE_SECRET_KEY:-}" == sk_live_* ]]; then
    fail "Staging must not use sk_live_ Stripe keys"
  fi
fi

if [[ $errors -gt 0 ]]; then
  echo "Validation failed with $errors error(s)"
  exit 1
fi

echo "Validation OK for $ENV_NAME ($FILE) — secret values not shown"
