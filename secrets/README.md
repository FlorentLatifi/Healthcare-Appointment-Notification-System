# Runtime secret files (optional Docker secrets)

Each file is a **single line** secret value (no quotes).

| File | Used for |
|------|----------|
| `jwt_secret` | `Jwt__Secret` |
| `db_password` | SQL Server SA / connection string password |
| `stripe_secret_key` | `Stripe__SecretKey` |
| `stripe_webhook_secret` | `Stripe__WebhookSecret` |
| `stripe_publishable_key` | `Stripe__PublishableKey` (live keys still sensitive) |
| `bootstrap_admin_password` | one-time bootstrap only |

Generate:

```bash
../scripts/generate-secrets.sh .
```

Materialize Compose env file:

```bash
../scripts/materialize-env-secrets.sh . > ../.env.secrets
chmod 600 ../.env.secrets
```

Permissions on the server:

```bash
chmod 700 .
chmod 600 ./*
```
