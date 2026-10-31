import AxeBuilder from '@axe-core/playwright';
import { Page, Route, expect, test } from '@playwright/test';

// create-user.feature, against a mocked API.
const text = {
  en: {
    newUser: 'New user',
    name: 'Name',
    email: 'Email',
    team: 'Team',
    create: 'Create user',
    copy: 'Copy link',
  },
  es: {
    newUser: 'Nuevo usuario',
    name: 'Nombre',
    email: 'Correo electrónico',
    team: 'Equipo',
    create: 'Crear usuario',
    copy: 'Copiar enlace',
  },
};

async function mockApi(page: Page): Promise<{ created: unknown[] }> {
  const created: unknown[] = [];
  await page.route('**/api/**', (route: Route) => {
    const request = route.request();
    const url = new URL(request.url()).pathname;
    switch (`${request.method()} ${url}`) {
      case 'POST /api/auth/refresh':
        return route.fulfill({ json: { accessToken: 'token', expiresAt: '2026-10-30T15:15:00Z' } });
      case 'GET /api/me':
        return route.fulfill({
          json: {
            id: 'ada',
            name: 'Ada',
            email: 'ada@clockin.test',
            role: 'Admin',
            permissions: [
              'users.manage',
              'teams.manage',
              'timesheets.review-all',
              'timesheets.manage',
            ],
            language: 'en',
            timeFormat: '24h',
          },
        });
      case 'GET /api/users':
        return route.fulfill({
          json: [
            {
              id: 'ada',
              name: 'Ada',
              email: 'ada@clockin.test',
              role: 'Admin',
              status: 'Active',
              teamId: null,
              teamName: null,
              ledTeamId: null,
              isLockedOut: false,
            },
          ],
        });
      case 'GET /api/teams':
        return route.fulfill({
          json: [
            {
              id: 'w',
              name: 'Warehouse',
              supervisorId: 'sam',
              supervisorName: 'Sam',
              isArchived: false,
              employeeCount: 2,
            },
          ],
        });
      case 'POST /api/users':
        created.push(request.postDataJSON());
        return route.fulfill({
          json: { token: 'fay-id.secret', expiresAt: '2026-11-02T15:00:00Z' },
        });
      default:
        return route.fulfill({ status: 404, json: { title: 'Not found' } });
    }
  });
  return { created };
}

test('an admin creates an employee and gets the activation link', async ({ page }, testInfo) => {
  const t = text[testInfo.project.name as keyof typeof text];
  const api = await mockApi(page);

  await page.goto('/users');
  await expect(page.getByRole('row', { name: /Ada/ })).toBeVisible();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);

  await page.getByRole('button', { name: t.newUser }).click();
  await page.getByLabel(t.name).fill('Fay');
  await page.getByLabel(t.email).fill('fay@clockin.test');
  await page.getByRole('combobox', { name: t.team }).click();
  await page.getByRole('option', { name: 'Warehouse' }).click();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.getByRole('button', { name: t.create }).click();

  await expect(page.getByRole('button', { name: t.copy })).toBeVisible();
  await expect(page.locator('code')).toContainText('set-password?token=fay-id.secret');
  expect(api.created).toEqual([
    {
      name: 'Fay',
      email: 'fay@clockin.test',
      role: 'Employee',
      teamId: 'w',
      language: 'en',
      timeFormat: '24h',
    },
  ]);
});
