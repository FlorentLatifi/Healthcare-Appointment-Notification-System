import { test, expect } from '@playwright/test';
import { installApiMock } from './fixtures/apiMock';

/**
 * Patient money path (API + Stripe mocked):
 * Register → Login → Create profile → Browse → Book slot → Pay → See appt → Cancel
 */
test.describe('Patient happy path', () => {
  test('register through cancel after mock payment', async ({ page }) => {
    await installApiMock(page);

    const stamp = Date.now();
    const username = `e2epat_${stamp}`;
    const password = 'SecurePass123!';

    // ── Register ─────────────────────────────────────────
    await page.goto('/register');
    const registerForm = page.getByRole('form', { name: /registration form/i });
    await expect(registerForm).toBeVisible();
    await registerForm.getByLabel('Username', { exact: true }).fill(username);
    await registerForm.getByLabel('Email', { exact: true }).fill(`${username}@test.com`);
    await registerForm.getByLabel('Password', { exact: true }).fill(password);
    await registerForm.getByLabel('Confirm Password', { exact: true }).fill(password);
    await registerForm.getByRole('button', { name: 'Register' }).click();
    await expect(page).toHaveURL(/\/login/);

    // ── Login ────────────────────────────────────────────
    const loginForm = page.getByRole('form', { name: /login form/i });
    await expect(loginForm).toBeVisible();
    await loginForm.getByLabel('Username', { exact: true }).fill(username);
    await loginForm.getByLabel('Password', { exact: true }).fill(password);
    await Promise.all([
      page.waitForURL(/\/(dashboard|create-patient)/, { timeout: 20_000 }),
      loginForm.getByRole('button', { name: 'Login' }).click(),
    ]);

    if (page.url().includes('/dashboard')) {
      await page.goto('/create-patient');
    }

    // ── Create patient profile ───────────────────────────
    await expect(page).toHaveURL(/create-patient/);
    const profileForm = page.getByRole('form', { name: /create patient profile form/i });
    await profileForm.getByLabel('First Name').fill('Pat');
    await profileForm.getByLabel('Last Name').fill('Patient');
    await profileForm.getByLabel('Email').fill(`${username}@test.com`);
    await profileForm.getByLabel('Phone Number').fill('+15559876543');
    await profileForm.getByLabel('Date of Birth').fill('1990-05-15');
    await profileForm.getByLabel('Street').fill('1 Test St');
    await profileForm.getByLabel('City').fill('Pristina');
    await profileForm.getByLabel('State').fill('KS');
    await profileForm.getByLabel('Postal Code').fill('10000');
    await profileForm.getByLabel('Country').fill('XK');
    await Promise.all([
      page.waitForURL(/\/dashboard/),
      profileForm.getByRole('button', { name: /create profile/i }).click(),
    ]);

    // ── Browse doctors ───────────────────────────────────
    await page.getByRole('button', { name: /^Doctors$/ }).click();
    await expect(page).toHaveURL(/\/doctors/);
    await expect(page.getByText(/Dr\.\s*Elena Rivera/i)).toBeVisible();
    await page.getByRole('button', { name: /Book Appointment/i }).first().click();
    await expect(page).toHaveURL(/\/book-appointment\/5/);

    // ── Book a free slot (far-future Monday) ─────────────
    await expect(page.getByRole('heading', { name: /book appointment/i })).toBeVisible();
    await page.locator('#appointmentDate').fill('2099-06-15');
    const slot = page.getByRole('option', { name: '10:00' });
    await expect(slot).toBeVisible({ timeout: 10_000 });
    await slot.click();
    await page.locator('textarea').fill('Annual checkup for E2E patient money path.');
    await Promise.all([
      page.waitForURL(/\/pay\/\d+/),
      page.getByRole('button', { name: /Book Appointment/i }).click(),
    ]);

    // ── Pay (Stripe mocked) ──────────────────────────────
    await expect(page.getByRole('heading', { name: /complete payment/i })).toBeVisible();
    await expect(page.getByTestId('e2e-mock-payment-form')).toBeVisible();
    await Promise.all([
      page.waitForURL(/\/my-appointments/),
      page.getByTestId('e2e-mock-pay-button').click(),
    ]);

    // ── See appointment ──────────────────────────────────
    await expect(page.getByText(/REF-E2E-/i).first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Cancel appointment/i })).toBeVisible();

    // ── Cancel ───────────────────────────────────────────
    await page.getByRole('button', { name: /Cancel appointment/i }).click();
    await page.getByLabel(/Cancellation reason/i).fill('Need a different time for E2E cancel flow.');
    await page.getByRole('button', { name: /Confirm cancel/i }).click();
    const rebook = page.getByTestId('rebook-prompt');
    await expect(rebook).toBeVisible({ timeout: 10_000 });
    await expect(rebook.getByText(/Appointment cancelled/i)).toBeVisible();
    await expect(rebook.getByRole('button', { name: /Book a new one/i })).toBeVisible();
  });
});
