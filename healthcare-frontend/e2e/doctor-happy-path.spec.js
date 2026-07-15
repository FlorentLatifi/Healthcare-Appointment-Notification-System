import { test, expect } from '@playwright/test';
import { installApiMock } from './fixtures/apiMock';

/**
 * Doctor workflow (API mocked):
 * Login → See pending → Confirm → Complete
 */
test.describe('Doctor happy path', () => {
  test('confirm then complete a pending appointment', async ({ page }) => {
    const state = await installApiMock(page, { mode: 'doctor' });
    const seeded = state.seedPendingAppointment();

    await page.goto('/login');
    const loginForm = page.getByRole('form', { name: /login form/i });
    await loginForm.getByLabel('Username', { exact: true }).fill('e2e_doctor');
    await loginForm.getByLabel('Password', { exact: true }).fill('SecurePass123!');
    await Promise.all([
      page.waitForURL(/\/(dashboard|doctor-dashboard)/, { timeout: 20_000 }),
      loginForm.getByRole('button', { name: 'Login' }).click(),
    ]);

    if (!page.url().includes('doctor-dashboard')) {
      await page.goto('/doctor-dashboard');
    }
    await expect(page).toHaveURL(/doctor-dashboard/);

    await expect(page.getByText(seeded.referenceCode, { exact: true })).toBeVisible();
    await expect(page.getByText(/Pat Patient/i)).toBeVisible();

    // ── Confirm ──────────────────────────────────────────
    await page.getByRole('button', { name: /Confirm appointment/i }).click();
    await page.getByRole('button', { name: /^Confirm$/ }).click();
    await expect(page.getByText('Confirmed', { exact: true }).first()).toBeVisible({ timeout: 10_000 });

    // ── Complete (Confirmed appointments) ────────────────
    const completeBtn = page.getByRole('button', { name: /Complete appointment/i });
    await expect(completeBtn).toBeVisible({ timeout: 10_000 });
    await completeBtn.click();

    await page.getByLabel(/Doctor Notes/i).fill(
      'Patient examined. Vitals stable. Follow up in six months as planned.',
    );
    await page.getByRole('button', { name: /^Complete$/ }).click();

    await expect(page.getByText('Completed', { exact: true }).first()).toBeVisible({ timeout: 10_000 });
  });
});
