import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const headings: Record<string, string> = {
  en: 'Ready to track your time',
  es: 'Listo para registrar tu tiempo',
};

test('shows the shell in the build language without accessibility violations', async ({
  page,
}, testInfo) => {
  const language = testInfo.project.name;

  await page.goto('/');

  await expect(page.locator('html')).toHaveAttribute('lang', language);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(headings[language]);
  const scan = await new AxeBuilder({ page }).analyze();
  expect(scan.violations).toEqual([]);
});
