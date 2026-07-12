# Local Docker — start the right way

## What you need

- Docker Desktop running (Windows)
- Repo root: folder that contains `docker-compose.yml` and `.env`

## 1. Create / fix `.env`

```powershell
cd path\to\Healthcare-Appointment-Notification-System
Copy-Item .env.example .env   # if .env missing
```

Minimum for a healthy stack:

| Variable | Required? | Notes |
|----------|-----------|--------|
| `DB_PASSWORD` | **Yes** | SQL SA password; keep strong |
| `JWT_SECRET` | **Yes** | ≥ 32 characters |
| `BOOTSTRAP_ADMIN_PASSWORD` | No | Empty = random password in API logs |
| `STRIPE_*` | For payments only | Real `sk_test_` / `pk_test_` / `whsec_` from Stripe |
| `VITE_API_BASE_URL` | Recommended | Default `http://localhost:5171/api/v1` |

**Do not use production secrets** in local `.env`.

## 2. Start

```powershell
docker compose up --build -d
docker compose ps
```

Wait until `sqlserver` and `redis` are **healthy**, `api` and `frontend` are **Up**.

## 3. Verify

```powershell
# API
curl http://localhost:5171/
# → {"status":"Healthy"}

# Frontend
# open http://localhost:5173
```

Admin bootstrap (first start only):

```powershell
docker compose logs api | Select-String "BOOTSTRAP ADMIN"
```

## 4. Important local vs production differences

| Topic | Local Docker (`ASPNETCORE_ENVIRONMENT=Docker`) | Production |
|-------|-----------------------------------------------|------------|
| TrustedProxies | Optional (warning only if missing) | **Required** — app fails to start |
| Stripe:WebhookSecret | Optional | **Required** — app fails to start |
| Swagger | Off | Off |
| Demo doctors | On via compose flags | Off |
| Admin create | Bootstrap / logs | Bootstrap secrets once, then disable |
| Public register | Patient / Doctor only (not Admin) | Same |

## 5. Stripe payments (optional)

1. Create a Stripe test account.
2. Put real test keys in `.env`:
   - `STRIPE_SECRET_KEY=sk_test_...`
   - `STRIPE_PUBLISHABLE_KEY=pk_test_...`
   - `STRIPE_WEBHOOK_SECRET=whsec_...` (for webhooks)
3. Rebuild frontend so Vite bakes the publishable key:

   ```powershell
   docker compose up --build -d frontend api
   ```

Without real keys, login + booking still work; card pay UI will log missing publishable key.

## 6. Common issues

| Symptom | Fix |
|---------|-----|
| Compose fails on empty password | Set `DB_PASSWORD` and `JWT_SECRET` in `.env` |
| API restarts / DB errors | Wait for SQL healthy; check `docker compose logs api` |
| Port 5171 / 5173 / 1433 in use | Stop other apps or change host ports in compose |
| Stale frontend API URL | Rebuild frontend (Vite is build-time) |
| Forgot admin password | `docker compose logs api` or set `BOOTSTRAP_ADMIN_PASSWORD` and recreate volume (`docker compose down -v` wipes DB) |

## 7. Stop / reset

```powershell
docker compose down          # keep data
docker compose down -v       # wipe SQL volume (fresh DB + re-bootstrap admin)
```

## 8. Without Docker (dev)

See root `README.md` → Manual. Use `appsettings.Development.json`, local SQL/Redis, `dotnet run`, and `npm run dev` in `healthcare-frontend` (Swagger works in Development).
