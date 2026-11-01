import AxeBuilder from '@axe-core/playwright';
import { Page, Route, expect, test } from '@playwright/test';

// approve-timesheet.feature and print-timesheet.feature, against a mocked API.
const text = {
  en: {
    review: 'Review',
    approve: 'Approve',
    approved: 'Approved',
    approvedBy: 'Approved by Sam',
    print: 'Print',
    mainNav: 'Main',
  },
  es: {
    review: 'Revisión',
    approve: 'Aprobar',
    approved: 'Aprobada',
    approvedBy: 'Aprobada por Sam',
    print: 'Imprimir',
    mainNav: 'Principal',
  },
};

const now = new Date('2026-10-30T15:00:00Z');
const eveId = '0192f0a4-7c1e-7b3a-9d2e-5f6a7b8c9d0e';
const can = {
  changeEntries: false,
  submit: false,
  approve: true,
  return: true,
  reopen: false,
  requestCorrection: false,
  decideCorrection: false,
};

async function mockApi(page: Page): Promise<void> {
  let approved = false;
  const timesheet = () => ({
    id: 't1',
    version: 2,
    ownerId: eveId,
    ownerName: 'Eve',
    teamName: 'Warehouse',
    weekStart: '2026-10-19',
    status: approved ? 'Approved' : 'Submitted',
    returnComment: null,
    approval: approved ? { approverName: 'Sam', at: now.toISOString() } : null,
    totalSeconds: 8 * 3600,
    days: [0, 1, 2, 3, 4, 5, 6].map((d) => ({
      date: `2026-10-${19 + d}`,
      seconds: d === 0 ? 8 * 3600 : 0,
      hasOpenEntry: false,
    })),
    entries: [
      {
        id: 'e1',
        date: '2026-10-19',
        start: '2026-10-19T13:00:00Z',
        end: '2026-10-19T21:00:00Z',
        seconds: 8 * 3600,
        manualChange: 'None',
        changedByName: null,
        note: null,
        punchedStart: null,
        punchedEnd: null,
      },
    ],
    history: [],
    latestCorrection: null,
    can: approved ? { ...can, approve: false, return: false } : can,
    timeZone: 'America/Bogota',
  });

  await page.route('**/api/**', (route: Route) => {
    const url = new URL(route.request().url()).pathname;
    switch (url) {
      case '/api/auth/refresh':
        return route.fulfill({ json: { accessToken: 'token', expiresAt: now.toISOString() } });
      case '/api/me':
        return route.fulfill({
          json: {
            id: 'sam',
            name: 'Sam',
            email: 'sam@clockin.test',
            role: 'Supervisor',
            permissions: ['time-entries.punch', 'timesheets.review-team'],
            language: 'en',
            timeFormat: '24h',
          },
        });
      case '/api/me/to-dos':
        return route.fulfill({
          json: {
            clockedInSince: null,
            returnedWeeks: [],
            unsubmittedWeeks: [],
            declinedCorrections: 0,
            timesheetsToReview: approved ? 0 : 1,
            correctionsToDecide: 0,
          },
        });
      case '/api/reports/review-queue':
        return route.fulfill({
          json: {
            hasMoreTimesheets: false,
            hasMoreCorrections: false,
            timesheets: approved
              ? []
              : [
                  {
                    timesheetId: 't1',
                    ownerId: eveId,
                    name: 'Eve',
                    teamName: 'Warehouse',
                    weekStart: '2026-10-19',
                    totalSeconds: 8 * 3600,
                    submittedAt: now.toISOString(),
                  },
                ],
            corrections: [],
          },
        });
      case `/api/timesheets/users/${eveId}/2026-10-19`:
        return route.fulfill({ json: timesheet() });
      case '/api/timesheets/t1/approve':
        approved = true;
        return route.fulfill({ status: 204 });
      default:
        return route.fulfill({ status: 404, json: { title: 'Not found' } });
    }
  });
}

test('a supervisor approves a submitted timesheet from the review queue and prints it', async ({
  page,
}, testInfo) => {
  const t = text[testInfo.project.name as keyof typeof text];
  await page.clock.install({ time: now });
  await mockApi(page);

  await page.goto('/review');
  const review = page.getByRole('tab', { name: new RegExp(t.review) });
  await expect(review).toContainText('1');
  await page.getByRole('link', { name: /Eve/ }).click();

  await expect(page).toHaveURL(new RegExp(`/review/${eveId}/2026-10-19$`));
  await expect(page.getByRole('heading', { name: 'Eve' })).toBeVisible();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);

  await page.getByRole('button', { name: t.approve }).click();

  await expect(page.getByText(t.approvedBy)).toBeVisible();
  await expect(page.locator('.status')).toHaveText(t.approved);
  await expect(page.getByRole('button', { name: t.approve })).toHaveCount(0);

  await page.emulateMedia({ media: 'print' });
  await expect(page.getByRole('button', { name: t.print })).toBeHidden();
  await expect(page.getByRole('navigation', { name: t.mainNav })).toBeHidden();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});

test('pages review work with accessible controls', async ({ page }, testInfo) => {
  const spanish = testInfo.project.name === 'es';
  await mockApi(page);
  await page.route('**/api/reports/review-queue?*', (route) => {
    const params = new URL(route.request().url()).searchParams;
    const second = params.get('timesheetPage') === '2';
    return route.fulfill({
      json: {
        timesheets: [
          {
            timesheetId: second ? 't2' : 't1',
            ownerId: eveId,
            name: second ? 'Ed' : 'Eve',
            teamName: 'Warehouse',
            weekStart: '2026-10-19',
            totalSeconds: 28800,
            submittedAt: null,
          },
        ],
        corrections: [],
        hasMoreTimesheets: !second,
        hasMoreCorrections: false,
      },
    });
  });
  await page.goto('/review');
  const pages = page.getByRole('navigation', {
    name: spanish ? 'Páginas de hojas de horas' : 'Timesheet pages',
  });
  await expect(
    pages.getByRole('button', { name: spanish ? 'Anterior' : 'Previous', exact: true }),
  ).toBeDisabled();
  await pages.getByRole('button', { name: spanish ? 'Siguiente' : 'Next', exact: true }).click();
  await expect(page.getByRole('link', { name: /Ed/ })).toBeVisible();
  await expect(pages).toContainText(spanish ? 'Página 2' : 'Page 2');
  await expect(
    pages.getByRole('button', { name: spanish ? 'Siguiente' : 'Next', exact: true }),
  ).toBeDisabled();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('review-pagination.png'), fullPage: true });
  await pages.getByRole('button', { name: spanish ? 'Anterior' : 'Previous', exact: true }).click();
  await expect(page.getByRole('link', { name: /Eve/ })).toBeVisible();
});
