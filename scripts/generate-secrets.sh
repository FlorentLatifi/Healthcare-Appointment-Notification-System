#!/usr/bin/env bash
# Generate strong secret files for production/staging hosts.
# Usage: ./scripts/generate-secrets.sh [output-dir]
set -euo pipefail

OUT="${1:-./secrets}"
mkdir -p "$OUT"
chmod 700 "$OUT" 2>/dev/null || true

rand() { openssl rand -base64 "$1" | tr -d '\n'; }

# SQL Server complexity: upper, lower, digit, special; length >= 16
sql_password() {
  local base special
  base="$(openssl rand -base64 24 | tr -d '/+=' | head -c 20)"
  special='!@#'
  # Ensure character classes
  echo "Aa1${special}${base}" | head -c 28
}

echo "Writing secrets under $OUT (not printed)"
printf '%s' "$(rand 48)" > "$OUT/jwt_secret"
printf '%s' "$(sql_password)" > "$OUT/db_password"
# Stripe keys must be pasted from dashboard — placeholders only
if [[ ! -f "$OUT/stripe_secret_key" ]]; then
  printf '%s' "sk_live_REPLACE_ME" > "$OUT/stripe_secret_key"
fi
if [[ ! -f "$OUT/stripe_publishable_key" ]]; then
  printf '%s' "pk_live_REPLACE_ME" > "$OUT/stripe_publishable_key"
fi
if [[ ! -f "$OUT/stripe_webhook_secret" ]]; then
  printf '%s' "whsec_REPLACE_ME" > "$OUT/stripe_webhook_secret"
fi

chmod 600 "$OUT"/* 2>/dev/null || true
echo "Done. Review Stripe placeholders, then run materialize-env-secrets.sh"
echo "  jwt_secret, db_password generated with openssl"
