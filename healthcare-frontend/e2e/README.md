# Playwright E2E (critical money paths)

Stable browser tests for the patient payment journey and doctor confirm/complete workflow.

## How they stay reliable

- **No live SQL / Redis / backend** — `e2e/fixtures/apiMock.js` intercepts all `/api/v1/**` calls.
- **No real Stripe** — builds with `VITE_E2E_MOCK_STRIPE=true` (see `.env.e2e`). The payment page shows a test-only mock pay button that still calls `POST /Payments/process` (mocked).
- Chromium only in CI for speed.

## Commands

From `healthcare-frontend/`:

```bash
# Install browser once (local or CI)
npx playwright install --with-deps chromium

# Run E2E (builds e2e mode + preview + tests)
npm run test:e2e

# Interactive UI mode
npm run test:e2e:ui
```

Or:

```bash
npx playwright test
```

## Specs

| File | Covers |
|------|--------|
| `patient-happy-path.spec.js` | Register → profile → browse → book slot → pay (mock) → appointments → cancel |
| `doctor-happy-path.spec.js` | Doctor login → pending appt → confirm → complete |

## CI

GitHub Actions job `frontend-e2e` in `.github/workflows/ci.yml` runs `npm run test:e2e` and is part of the CI gate.
