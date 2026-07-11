#!/bin/sh
# Load Docker secret files into ASP.NET environment variables when present.
# Prefer /run/secrets/* (Compose secrets) over existing env.
# Then start the API.
set -eu

load_secret() {
  # $1 = file path under /run/secrets, $2 = env var name
  _path="/run/secrets/$1"
  if [ -f "$_path" ]; then
    # Trim trailing newlines
    _val=$(tr -d '\r\n' < "$_path")
    if [ -n "$_val" ]; then
      export "$2=$_val"
    fi
  fi
}

load_secret jwt_secret Jwt__Secret
load_secret db_password DB_PASSWORD
load_secret stripe_secret_key Stripe__SecretKey
load_secret stripe_publishable_key Stripe__PublishableKey
load_secret stripe_webhook_secret Stripe__WebhookSecret
load_secret bootstrap_admin_password Seeding__BootstrapAdmin__Password

# If connection string not fully provided, build from DB_PASSWORD (Compose default pattern)
if [ -z "${ConnectionStrings__DefaultConnection:-}" ] && [ -n "${DB_PASSWORD:-}" ]; then
  export ConnectionStrings__DefaultConnection="Server=sqlserver;Database=${DB_NAME:-HealthcareDb};User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true;MultipleActiveResultSets=true"
fi

exec dotnet Healthcare.Presentation.API.dll
