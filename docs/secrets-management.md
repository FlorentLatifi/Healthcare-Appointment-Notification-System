# Secrets Management Strategy (Production)

## Goals

| Goal | Why |
|------|-----|
| No secrets in git / images | Avoid permanent leakage in history & registries |
| Separate **config** from **secrets** | Config can be reviewed; secrets must be tightly controlled |
| Staging ≠ production secrets | Limit blast radius of a leak |
| Fail fast if missing | Better than running with empty JWT / open DB |
| Solo/small-team operable | Low ops burden, no full-time platform engineer |
| Upgrade path | Can move to a vault later without rewriting the app |

---

## Option comparison

| Option | Fit for solo/small team | Cost / complexity | Strengths | Weaknesses |
|--------|-------------------------|-------------------|-----------|------------|
| **GitHub Secrets + Environments** | ★★★★★ **Recommended base** | Free, already in use | Env isolation, approval gates, OIDC-ready, no new vendor | Runtime host still needs a local secret materialization; not a runtime vault |
| **Docker Compose `env_file` + host files (0600)** | ★★★★★ **Recommended runtime** | Free | Simple, works on 1 VM, no swarm required | Operator must protect disk + backups; no automatic rotation UI |
| **Docker Swarm / Compose secrets (`/run/secrets`)** | ★★★★ | Free | Secrets not in process `environ` dump as easily; tmpfs | Slightly more setup; SQL Server still needs password in env |
| **Doppler** | ★★★★ | Free tier → paid | Excellent DX, sync to GH/Compose, audit, rotation helpers | Third-party dependency; PHI-adjacent systems may need vendor review |
| **Azure Key Vault** | ★★★ | $ + Azure learning | Enterprise RBAC, soft-delete, HSM, MSI auth | Overkill on pure Compose/VM; needs Azure identity wiring |
| **HashiCorp Vault** | ★★ | Ops-heavy | Best-in-class policies, dynamic DB creds | Run/maintain Vault (or HCP); high complexity for solo |
| **AWS Secrets Manager / SSM** | ★★★ | $ + AWS | Solid if already on AWS | Same “cloud lock-in” as Key Vault |
| **.env committed / user-secrets in prod** | ★ | — | Easy | **Never for production** |
| **Kubernetes Secrets (plain)** | ★★★ if on K8s | K8s required | Native | Base64 ≠ encryption; need sealed-secrets/ESO/KMS |

### Recommendation (solo / small team)

**Use a two-layer model:**

```
┌─────────────────────────────────────────────────────────┐
│  Layer A — CI/CD source of truth                         │
│  GitHub Environments: staging | production               │
│  • Required reviewers on production                      │
│  • Distinct secrets per environment                      │
└───────────────────────────┬─────────────────────────────┘
                            │ deploy job materializes
                            ▼
┌─────────────────────────────────────────────────────────┐
│  Layer B — Runtime on the VM                             │
│  /opt/healthcare/                                        │
│    .env.secrets   (0600)  ← JWT, DB, Stripe, bootstrap   │
│    config/production.env  ← non-secret config (ok in git)│
│    secrets/*.txt (optional Docker secret files)          │
│  docker compose --env-file .env.secrets -f prod up       │
└─────────────────────────────────────────────────────────┘
```

**Why this wins for you now**

1. You already deploy with Compose on a VM — no K8s tax.
2. GitHub Environments give free approval gates + isolation.
3. Host files are simple to audit (`ls -la`, `stat`).
4. App stays vanilla ASP.NET Core configuration (`Jwt__Secret`, etc.).
5. When you outgrow it: point Layer A at **Doppler** or **Key Vault** and keep the same env var names (zero app rewrite).

**When to upgrade**

| Trigger | Next step |
|---------|-----------|
| 3+ people need secret access / audit logs | **Doppler** (fastest) or Key Vault |
| Move to Azure Container Apps / AKS | **Azure Key Vault** + Managed Identity |
| Need dynamic SQL credentials / short-lived tokens | **Vault** or cloud secret manager |
| Multi-cloud / strict compliance (SOC2 evidence) | Vault or cloud KMS-backed store |

---

## Environment variable strategy

### Naming (ASP.NET Core)

| Concern | Env var | Config key |
|---------|---------|------------|
| SQL connection | `ConnectionStrings__DefaultConnection` **or** parts + compose build | `ConnectionStrings:DefaultConnection` |
| SQL SA (compose only) | `DB_PASSWORD` | used by compose → connection string |
| JWT signing key | `Jwt__Secret` | `Jwt:Secret` |
| JWT issuer/audience | `Jwt__Issuer`, `Jwt__Audience` | non-secret-ish; can live in config file |
| Stripe secret | `Stripe__SecretKey` | `Stripe:SecretKey` |
| Stripe publishable | `Stripe__PublishableKey` | public-ish; still not in git for live keys |
| Stripe webhook | `Stripe__WebhookSecret` | `Stripe:WebhookSecret` |
| Redis | `Redis__ConnectionString` | non-secret host string OK on private network |
| Bootstrap admin | `Seeding__BootstrapAdmin__Password` etc. | one-time; disable after use |

**Rules**

1. **`appsettings.json` / `appsettings.Production.json`**: never store real secrets (empty or placeholders only).
2. **Non-secrets** (`Issuer`, `BatchSize`, feature flags): `config/production.env` or appsettings.
3. **Secrets**: only `.env.secrets` / Docker secret files / vault — never image layers.
4. **Double underscore** `__` for hierarchy in env vars.
5. Prefer **separate** staging vs production values for every secret.
6. Prefer **connection string from compose** so password is not duplicated in two formats when avoidable.

### Classification

| Class | Examples | Storage |
|-------|----------|---------|
| **Secret** | `Jwt__Secret`, `DB_PASSWORD`, `Stripe__SecretKey`, `Stripe__WebhookSecret`, bootstrap password, GHCR pull token, SSH key | GitHub Environment secrets → host `.env.secrets` |
| **Sensitive config** | `Stripe__PublishableKey` (live), `FRONTEND_URL` | Env secrets or restricted config |
| **Public / low risk** | `Jwt__Issuer`, feature flags, log levels, Outbox intervals | Git + `config/production.env` |

---

## Handling specific secrets

### `Jwt__Secret`

- Generate: `openssl rand -base64 48` (≥ 32 chars required by app).
- **Never** reuse staging ↔ production.
- Rotation: generate new secret → deploy → **all access tokens invalid** (users re-login). Schedule maintenance window; refresh tokens also depend on server-side store (Redis) so users re-auth.
- Fail-fast: `JwtSettings.FromConfiguration` already rejects missing/short secrets.

### Database password (`DB_PASSWORD`)

- Strong random (≥ 16 chars, SQL Server complexity rules).
- Only on Docker network (compose does not publish `1433` in prod).
- Prefer dedicated SQL login later (not `sa`) when you harden further.
- Backups of the volume are as sensitive as the password — encrypt backup media.

### Stripe keys

| Key | Staging | Production |
|-----|---------|------------|
| `Stripe__SecretKey` | `sk_test_…` | `sk_live_…` |
| `Stripe__PublishableKey` | `pk_test_…` | `pk_live_…` |
| `Stripe__WebhookSecret` | test endpoint secret | live endpoint secret |

- Webhook secret validates Stripe signatures — treat as secret.
- Never log request bodies that include full PaymentIntent client secrets.

### Bootstrap admin password

- Enable **once** via secrets; then set `BOOTSTRAP_ADMIN_ENABLED=false`.
- Reject well-known passwords (already enforced in app).
- Prefer SSO later; bootstrap is for day-1 only.

### Redis

- No password in current compose (private network only). Optional upgrade: Redis ACL + password in secret file.
- Cache keys must not contain secrets/PII (already keyed by ids).

---

## Runtime layout on the VM

```text
/opt/healthcare/                     # DEPLOY_PATH
├── docker-compose.prod.yml
├── scripts/remote-deploy.sh
├── config/
│   └── production.env               # non-secret (may be from git)
├── .env.secrets                     # 0600 — secrets only (from GH Actions or operator)
├── .deploy-state                    # 0600 — previous image tags (not secrets)
└── secrets/                         # optional file-based Docker secrets
    ├── jwt_secret                   # 0400, single line, no newline junk
    ├── db_password
    ├── stripe_secret_key
    └── stripe_webhook_secret
```

### Deploy path (GitHub Actions)

1. Job in Environment `production` reads environment secrets.
2. Writes runner-local file (umask 077) — **not** `ssh host "export JWT=..."`.
3. `scp` to `/opt/healthcare/.env.secrets` mode `600`.
4. `remote-deploy.sh` runs `docker compose --env-file .env.secrets …`.
5. Runner shreds local key/env files.

### Manual path (operator)

```bash
./scripts/generate-secrets.sh ./secrets
# edit stripe keys
./scripts/materialize-env-secrets.sh ./secrets > /opt/healthcare/.env.secrets
chmod 600 /opt/healthcare/.env.secrets
```

---

## What we deliberately do **not** do

- Put secrets in `appsettings.Production.json` in the image.
- Use `latest` tags as the secret-rotation mechanism.
- Share one GitHub repo secret between staging and production.
- Print secrets in workflow logs (`echo $JWT_SECRET`).
- Mount the whole home directory into containers.

---

## Optional next step: Doppler (lowest-friction vault)

```bash
# On the server (conceptually)
doppler secrets download --no-file --format env > .env.secrets
chmod 600 .env.secrets
```

Keep the same variable names → zero app changes.

## Optional next step: Azure Key Vault

Wire at app startup:

```csharp
builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
```

Map secret names `Jwt--Secret` → `Jwt:Secret`. Use only when the API identity is already on Azure.
