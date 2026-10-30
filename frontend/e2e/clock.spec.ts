import AxeBuilder from '@axe-core/playwright';
import { Page, expect, test } from '@playwright/test';

// docs/specs/reporting/clock-screen.feature, against a mocked API.
const text = {
  en: {
    signIn: 'Sign in',
    email: 'Email',
    password: 'Password',
    clockIn: 'Clock in',
    clockOut: 'Clock out',
    since: 'Clocked in since 08:00',
    today: 'Today',
  },
  es: {
    signIn: 'Iniciar sesión',
    email: 'Correo electrónico',
    password: 'Contraseña',
    clockIn: 'Marcar entrada',
    clockOut: 'Marcar salida',
    since: 'Con entrada desde 08:00',
    today: 'Hoy',
  },
};

// 10:00 in Bogotá; Eve clocks in at 08:00.
const now = new Date('2026-10-30T15:00:00Z');
const openEntry = {
  id: 'e1',
  timesheetId: 't1',
  start: '2026-10-30T13:00:00Z',
  end: null,
  isManuallyChanged: false,
};

async function mockApi(page: Page): Promise<void> {
  let clockedIn = false;
  const status = () => ({
    openEntry: clockedIn ? openEntry : null,
    openEntryStartedToday: clockedIn,
    openEntryStartedThisWeek: clockedIn,
    todayEntries: clockedIn ? [openEntry] : [],
    closedTodaySeconds: 0,
    closedWeekSeconds: 4 * 3600,
    weekStart: '2026-10-26',
    timeZone: 'America/Bogota',
  });

  await page.route('**/api/**', (route) => {
    const url = new URL(route.request().url()).pathname;
    switch (url) {
      case '/api/auth/refresh':
        return route.fulfill({ status: 401, json: { title: 'Unauthorized' } });
      case '/api/auth/login':
        return route.fulfill({ json: { accessToken: 'token', expiresAt: now.toISOString() } });
      case '/api/me':
        return route.fulfill({
          json: {
            id: 'u1',
            name: 'Eve',
            email: 'eve@clockin.test',
            role: 'Employee',
            permissions: ['time-entries.punch'],
          },
        });
      case '/api/time-entries/clock':
        return route.fulfill({ json: status() });
      case '/api/time-entries/clock-in':
        clockedIn = true;
        return route.fulfill({ status: 204 });
      default:
        return route.fulfill({ status: 404 });
    }
  });
}

test('signs in, clocks in, and shows the running time without accessibility violations', async ({
  page,
}, testInfo) => {
  const t = text[testInfo.project.name as keyof typeof text];
  await page.clock.install({ time: now });
  await page.setViewportSize({ width: 375, height: 800 });
  await mockApi(page);

  await page.goto('/');
  await expect(page).toHaveURL(/\/sign-in$/);
  await expect(page.locator('html')).toHaveAttribute('lang', testInfo.project.name);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);

  await page.getByLabel(t.email).fill('eve@clockin.test');
  await page.getByLabel(t.password).fill('correct horse battery');
  await page.getByRole('button', { name: t.signIn }).click();

  await expect(page).toHaveURL(/\/clock$/);
  await expect(page.getByRole('button', { name: t.clockIn })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(375);

  await page.getByRole('button', { name: t.clockIn }).click();

  await expect(page.getByRole('button', { name: t.clockOut })).toBeVisible();
  await expect(page.getByText(t.since)).toBeVisible();
  await expect(page.locator('.total').first()).toHaveText(`${t.today}2:00`);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});
